using System.Security.Claims;

namespace GeenGrens.ApiService.Controllers;

[Route("/api/game")]
[ApiController]
public class GameElementController(
    UserManager<UserModel> userManager,
    GeenGrensContext dbContext) : ControllerBase
{
    // ────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────

    private async Task<UserModel?> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId == null ? null : await userManager.FindByIdAsync(userId);
    }

    /// <summary>
    /// Gets (or lazily creates) the TeamProgress record for a team.
    /// </summary>
    private async Task<TeamProgressModel> GetOrCreateTeamProgress(int teamId)
    {
        var progress = await dbContext.TeamProgresss
            .FirstOrDefaultAsync(p => p.TeamId == teamId);

        if (progress == null)
        {
            progress = new TeamProgressModel { TeamId = teamId };
            dbContext.TeamProgresss.Add(progress);
            await dbContext.SaveChangesAsync();
        }

        return progress;
    }

    // ────────────────────────────────────────────────────────────
    // Endpoints
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// The player enters the password found in Viktor's notebook.
    /// On success, returns the physical location of the notebook.
    /// The location is team-specific so each team can find their own copy.
    /// </summary>
    [Authorize]
    [HttpPost("VerifyPassword")]
    public async Task<IActionResult> VerifyPassword([FromBody] PasswordDTO dto)
    {
        if (dto.Password != "9360" && dto.Password != "0639")
            return Ok(new { success = false, notebookLocation = "" });

        var user = await GetCurrentUser();
        string notebookLocation = "Onder de trap"; // sensible default

        if (user?.TeamId > 0)
        {
            var team = await dbContext.Teams.FindAsync(user.TeamId);
            if (team?.NotebookLocation != null)
                notebookLocation = team.NotebookLocation;

            var progress = await GetOrCreateTeamProgress(user.TeamId);
            if (!progress.IsNotebookUnlocked)
            {
                progress.IsNotebookUnlocked = true;
                await dbContext.SaveChangesAsync();
            }
        }

        return Ok(new { success = true, notebookLocation });
    }

    /// <summary>
    /// Returns the current game state for the authenticated player's team:
    /// notebook unlock status, notebook location, and feature access flags.
    /// </summary>
    [Authorize]
    [HttpGet("Status")]
    public async Task<IActionResult> Status()
    {
        var user = await GetCurrentUser();

        if (user == null || user.TeamId == 0)
            return Ok(new
            {
                isUnlocked = false,
                notebookLocation = (string?)null,
                canAccessChat = false,
                canSubmitTip = false,
            });

        var team = await dbContext.Teams.FindAsync(user.TeamId);
        var progress = await GetOrCreateTeamProgress(user.TeamId);

        return Ok(new
        {
            isUnlocked = progress.IsNotebookUnlocked,
            notebookLocation = progress.IsNotebookUnlocked
                ? (team?.NotebookLocation ?? "Onder de trap")
                : (string?)null,
            canAccessChat = progress.CanAccessChat,
            canSubmitTip = progress.CanSubmitTip,
            isPlaytest = team?.IsPlaytest ?? false,
        });
    }

    /// <summary>
    /// Returns just the notebook location for the current team.
    /// </summary>
    [Authorize]
    [HttpGet("NotebookLocation")]
    public async Task<IActionResult> NotebookLocation()
    {
        var user = await GetCurrentUser();
        if (user?.TeamId > 0)
        {
            var team = await dbContext.Teams.FindAsync(user.TeamId);
            return Ok(new { location = team?.NotebookLocation ?? "Onder de trap" });
        }
        return Ok(new { location = "Onder de trap" });
    }

    /// <summary>
    /// Returns the characters that the current team has unlocked via location codes.
    /// Used by the chat page to only show accessible suspects.
    /// </summary>
    [Authorize]
    [HttpGet("UnlockedCharacters")]
    public async Task<IActionResult> UnlockedCharacters()
    {
        var user = await GetCurrentUser();
        if (user == null || user.TeamId == 0)
            return Ok(new List<object>());

        var unlockedCharacterIds = await dbContext.TeamUnlocks
            .Where(u => u.TeamId == user.TeamId)
            .Include(u => u.LocationCode)
            .Select(u => u.LocationCode.CharacterId)
            .Distinct()
            .ToListAsync();

        if (!unlockedCharacterIds.Any())
            return Ok(new List<object>());

        var characters = await dbContext.Characters
            .Where(c => unlockedCharacterIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                c.AvatarUrl,
                c.Personality,
            })
            .ToListAsync();

        return Ok(characters);
    }

    // ────────────────────────────────────────────────────────────
    // DTOs
    // ────────────────────────────────────────────────────────────
    public class PasswordDTO
    {
        public string Password { get; set; } = string.Empty;
    }
}
