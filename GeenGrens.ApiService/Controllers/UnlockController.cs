using System.Security.Claims;

namespace GeenGrens.ApiService.Controllers;

/// <summary>
/// Handles players entering location codes they found in Baarle-Nassau.
/// Each valid code unlocks a suspect character for interrogation.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UnlockController(
    UserManager<UserModel> userManager,
    GeenGrensContext dbContext) : ControllerBase
{
    // ────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────

    private async Task<int> GetTeamId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return 0;
        var user = await userManager.FindByIdAsync(userId);
        return user?.TeamId ?? 0;
    }

    // ────────────────────────────────────────────────────────────
    // Endpoints
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enter a location code. On success, records the unlock for this team
    /// and returns the unlocked character info.
    /// </summary>
    [HttpPost("EnterCode")]
    public async Task<IActionResult> EnterCode([FromBody] EnterCodeDTO dto)
    {
        var teamId = await GetTeamId();
        if (teamId == 0)
            return BadRequest(new { success = false, message = "Je bent niet aan een team gekoppeld. Neem contact op met de spelleider." });

        var code = dto.Code?.ToUpperInvariant().Trim();
        if (string.IsNullOrEmpty(code))
            return BadRequest(new { success = false, message = "Voer een code in." });

        var locationCode = await dbContext.LocationCodes
            .Include(l => l.Character)
            .FirstOrDefaultAsync(l => l.Code == code);

        if (locationCode == null)
            return Ok(new { success = false, message = "Ongeldige code. Controleer of je de juiste locatie hebt gevonden." });

        // Prevent double-entering the same code
        var alreadyUnlocked = await dbContext.TeamUnlocks
            .AnyAsync(u => u.TeamId == teamId && u.LocationCodeId == locationCode.Id);

        if (alreadyUnlocked)
            return Ok(new { success = false, message = "Deze code heb je al gebruikt." });

        // Record the unlock
        dbContext.TeamUnlocks.Add(new TeamUnlockModel
        {
            TeamId = teamId,
            LocationCodeId = locationCode.Id,
            UnlockedAt = DateTime.UtcNow,
        });

        // Update team progress flags
        var progress = await dbContext.TeamProgresss.FirstOrDefaultAsync(p => p.TeamId == teamId);
        if (progress == null)
        {
            progress = new TeamProgressModel { TeamId = teamId };
            dbContext.TeamProgresss.Add(progress);
        }

        // First code entered → enable chat
        progress.CanAccessChat = true;

        // All codes entered → enable tip submission
        var totalCodes = await dbContext.LocationCodes.CountAsync();
        // +1 because the current unlock hasn't been saved yet
        var teamUnlockCount = await dbContext.TeamUnlocks.CountAsync(u => u.TeamId == teamId) + 1;
        if (totalCodes > 0 && teamUnlockCount >= totalCodes)
            progress.CanSubmitTip = true;

        await dbContext.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = locationCode.UnlockMessage,
            characterName = locationCode.Character?.Name,
            characterId = locationCode.CharacterId,
            locationName = locationCode.LocationName,
        });
    }

    /// <summary>
    /// Returns all location codes the current team has already unlocked.
    /// </summary>
    [HttpGet("GetUnlocked")]
    public async Task<IActionResult> GetUnlocked()
    {
        var teamId = await GetTeamId();
        if (teamId == 0)
            return Ok(Array.Empty<object>());

        var unlocks = await dbContext.TeamUnlocks
            .Where(u => u.TeamId == teamId)
            .Include(u => u.LocationCode)
                .ThenInclude(l => l.Character)
            .OrderBy(u => u.UnlockedAt)
            .Select(u => new
            {
                code = u.LocationCode.Code,
                locationName = u.LocationCode.LocationName,
                characterName = u.LocationCode.Character != null ? u.LocationCode.Character.Name : null,
                characterId = u.LocationCode.CharacterId,
                unlockedAt = u.UnlockedAt,
            })
            .ToListAsync();

        return Ok(unlocks);
    }

    // ────────────────────────────────────────────────────────────
    // DTOs
    // ────────────────────────────────────────────────────────────
    public class EnterCodeDTO
    {
        public string Code { get; set; } = string.Empty;
    }
}
