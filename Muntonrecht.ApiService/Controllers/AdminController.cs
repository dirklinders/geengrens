using System.Security.Claims;

namespace Muntonrecht.ApiService.Controllers;

/// <summary>
/// Admin-only endpoints for managing teams, assigning players, and viewing game progress.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "admin")]
public class AdminController(
    UserManager<UserModel> userManager,
    RoleManager<IdentityRole> roleManager,
    MuntonrechtContext dbContext) : ControllerBase
{
    // ────────────────────────────────────────────────────────────
    // User management
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all registered users with their team assignment.
    /// </summary>
    [HttpGet("Users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = userManager.Users.ToList();
        var teams = await dbContext.Teams.ToListAsync();

        var result = users.Select(u => new
        {
            id = u.Id,
            email = u.Email,
            name = u.FullName,
            teamId = u.TeamId,
            teamName = teams.FirstOrDefault(t => t.Id == u.TeamId)?.Name,
        });

        return Ok(result);
    }

    /// <summary>
    /// Assigns a user to a team. Pass teamId = 0 to remove team assignment.
    /// </summary>
    [HttpPost("AssignTeam")]
    public async Task<IActionResult> AssignTeam([FromBody] AssignTeamDTO dto)
    {
        var user = await userManager.FindByIdAsync(dto.UserId);
        if (user == null) return NotFound("Gebruiker niet gevonden.");

        user.TeamId = dto.TeamId;
        var result = await userManager.UpdateAsync(user);

        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }

    /// <summary>
    /// Creates a new user with email and password.
    /// </summary>
    [HttpPost("CreateUser")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
    {
        var user = new UserModel
        {
            UserName = dto.Email,
            Email = dto.Email,
            NormalizedEmail = dto.Email.ToUpperInvariant(),
            FullName = dto.Name ?? dto.Email,
            EmailConfirmed = true,
            TeamId = 0,
        };

        var result = await userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        return Ok(new { id = user.Id, email = user.Email, name = user.FullName });
    }

    /// <summary>
    /// Deletes a user by ID.
    /// </summary>
    [HttpDelete("DeleteUser/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return NotFound("Gebruiker niet gevonden.");

        var result = await userManager.DeleteAsync(user);
        return result.Succeeded ? Ok() : BadRequest(result.Errors.Select(e => e.Description));
    }

    /// <summary>
    /// Promotes a user to the admin role.
    /// </summary>
    [HttpPost("MakeAdmin")]
    public async Task<IActionResult> MakeAdmin([FromBody] UserIdDTO dto)
    {
        var user = await userManager.FindByIdAsync(dto.UserId);
        if (user == null) return NotFound("Gebruiker niet gevonden.");

        if (!await roleManager.RoleExistsAsync("admin"))
            await roleManager.CreateAsync(new IdentityRole("admin"));

        await userManager.AddToRoleAsync(user, "admin");
        return Ok();
    }

    // ────────────────────────────────────────────────────────────
    // Progress overview
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a full progress overview for every team:
    /// notebook status, chat/tip access, submitted tip, unlocked codes.
    /// </summary>
    [HttpGet("TeamProgress")]
    public async Task<IActionResult> GetTeamProgress()
    {
        var teams = await dbContext.Teams.ToListAsync();
        var progresses = await dbContext.TeamProgresss.ToListAsync();
        var unlocks = await dbContext.TeamUnlocks
            .Include(u => u.LocationCode)
            .ToListAsync();

        var users = userManager.Users.ToList();

        var result = teams.Select(t =>
        {
            var prog = progresses.FirstOrDefault(p => p.TeamId == t.Id);
            var teamUnlocks = unlocks.Where(u => u.TeamId == t.Id).ToList();
            var members = users.Where(u => u.TeamId == t.Id).ToList();

            return new
            {
                teamId = t.Id,
                teamName = t.Name,
                members = members.Select(m => new { m.Id, m.Email, m.FullName }),
                progress = prog == null ? null : new
                {
                    canAccessChat = prog.CanAccessChat,
                    canSubmitTip = prog.CanSubmitTip,
                    tipSubmitted = prog.TipSubmitted,
                    tipSuspectId = prog.TipSuspectId,
                    tipWeaponId = prog.TipWeaponId,
                    tipLocationId = prog.TipLocationId,
                    tipIsCorrect = prog.TipIsCorrect,
                },
                unlockedCount = teamUnlocks.Count,
                unlockedCodes = teamUnlocks.Select(u => new
                {
                    code = u.LocationCode?.Code,
                    locationName = u.LocationCode?.LocationName,
                    unlockedAt = u.UnlockedAt,
                }),
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// Manually overrides the progress flags for a team.
    /// Useful for fixing edge cases or helping stuck teams.
    /// </summary>
    [HttpPost("SetProgress")]
    public async Task<IActionResult> SetProgress([FromBody] SetProgressDTO dto)
    {
        var progress = await dbContext.TeamProgresss
            .Include(p => p.Location)
            .FirstOrDefaultAsync(p => p.TeamId == dto.TeamId);
        if (progress == null)
        {
            progress = new TeamProgressModel { TeamId = dto.TeamId };
            dbContext.TeamProgresss.Add(progress);
        }

        if (dto.CanAccessChat.HasValue) progress.CanAccessChat = dto.CanAccessChat.Value;
        if (dto.CanSubmitTip.HasValue) progress.CanSubmitTip = dto.CanSubmitTip.Value;
        if (dto.LocationId.HasValue) progress.LocationId = dto.LocationId.Value;
        if (dto.WeaponId.HasValue) progress.WeaponId = dto.WeaponId.Value;

        await dbContext.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// Pre-assigns a location to a team. The team will automatically
    /// have access to the character at that location.
    /// </summary>
    [HttpPost("AssignLocation")]
    public async Task<IActionResult> AssignLocation([FromBody] AssignLocationDTO dto)
    {
        var progress = await dbContext.TeamProgresss.FirstOrDefaultAsync(p => p.TeamId == dto.TeamId);
        if (progress == null)
        {
            progress = new TeamProgressModel { TeamId = dto.TeamId };
            dbContext.TeamProgresss.Add(progress);
        }

        progress.LocationId = dto.LocationId;
        progress.CanAccessChat = true;

        await dbContext.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// Re-syncs the logigram grid from the game content: every character
    /// (suspect), weapon and location becomes an entry of the matching standard
    /// category ("suspect" | "weapon" | "location"). Legacy manual entries are
    /// adopted by name or removed; entry ids are preserved so team marks keep
    /// working. The player logigram endpoint also syncs automatically — this
    /// endpoint lets the admin trigger the same sync on demand.
    /// </summary>
    [HttpPost("Logigram/Sync")]
    public async Task<IActionResult> SyncLogigram()
    {
        var result = await LogigramSyncManager.SyncFromGameElementsAsync(dbContext);
        return Ok(new { result.Created, result.Updated, result.Deleted, result.Adopted });
    }

    /// <summary>
    /// Returns a single team's full detail: progress flags, unlocked location
    /// codes, and — for each unlocked character — the full chat transcript.
    /// </summary>
    [HttpGet("TeamDetail/{teamId}")]
    public async Task<IActionResult> GetTeamDetail(int teamId)
    {
        var team = await dbContext.Teams.FindAsync(teamId);
        if (team == null) return NotFound("Team niet gevonden.");

        var progress = await dbContext.TeamProgresss
            .FirstOrDefaultAsync(p => p.TeamId == teamId);

        var members = userManager.Users
            .Where(u => u.TeamId == teamId)
            .Select(u => new { id = u.Id, email = u.Email, fullName = u.FullName })
            .ToList();

        // Unlocks with the map location (and its suspect) eager-loaded
        var unlocks = await dbContext.TeamUnlocks
            .Where(u => u.TeamId == teamId)
            .Include(u => u.LocationCode)
                .ThenInclude(lc => lc.Location)
                    .ThenInclude(l => l.Character)
            .OrderBy(u => u.UnlockedAt)
            .ToListAsync();

        // All chats for this team, grouped by character
        var chats = await dbContext.Chats
            .Where(c => c.TeamId == teamId)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var chatsByCharacter = chats
            .GroupBy(c => c.CharacterId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => new { role = c.Role, message = c.Message }).ToList()
            );

        var unlockedCodes = unlocks.Select(u =>
        {
            var lc = u.LocationCode;
            // Suspect found at the unlocked location (null when none linked)
            var ch = lc?.Location?.Character;
            var chatList = ch != null && chatsByCharacter.TryGetValue(ch.Id, out var cl) ? cl : [];
            return new
            {
                code          = lc?.Code,
                locationName  = lc?.LocationName,
                unlockedAt    = u.UnlockedAt,
                characterId   = ch?.Id,
                characterName = ch?.Name,
                chats         = chatList,
            };
        });

        return Ok(new
        {
            teamId           = team.Id,
            teamName         = team.Name,
            members,
            progress = progress == null ? null : new
            {
                canAccessChat      = progress.CanAccessChat,
                canSubmitTip       = progress.CanSubmitTip,
                tipSubmitted       = progress.TipSubmitted,
                tipSuspectId       = progress.TipSuspectId,
                tipWeaponId        = progress.TipWeaponId,
                tipLocationId      = progress.TipLocationId,
                tipIsCorrect       = progress.TipIsCorrect,
            },
            unlockedCodes,
        });
    }

    // ────────────────────────────────────────────────────────────
    // DTOs
    // ────────────────────────────────────────────────────────────
    public class AssignTeamDTO
    {
        public string UserId { get; set; } = string.Empty;
        public int TeamId { get; set; }
    }

    public class UserIdDTO
    {
        public string UserId { get; set; } = string.Empty;
    }

    public class SetProgressDTO
    {
        public int TeamId { get; set; }
        public bool? CanAccessChat { get; set; }
        public bool? CanSubmitTip { get; set; }
        public int? LocationId { get; set; }
        public int? WeaponId { get; set; }
    }

    public class AssignLocationDTO
    {
        public int TeamId { get; set; }
        public int LocationId { get; set; }
    }

    public class CreateUserDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Name { get; set; }
    }
}
