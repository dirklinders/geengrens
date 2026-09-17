using System.Security.Claims;

namespace Muntonrecht.ApiService.Controllers;

/// <summary>
/// Handles players entering location codes they found in Zutphen.
/// Each valid code unlocks the linked map location (its dossier on the
/// Onderzoek tab and, when chat is enabled, the suspect found there).
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UnlockController(
    UserManager<UserModel> userManager,
    MuntonrechtContext dbContext,
    IConfiguration configuration) : ControllerBase
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

    // ────────────────────────────────────────────────────────────
    // NFC scan redirect
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// NFC tag entry point. The tag URL is: /api/Unlock/{nfcCode}
    /// - Not authenticated  → redirect to login page with returnUrl
    /// - No team assigned   → redirect to home (player needs to be set up first)
    /// - Code not found     → redirect to /unlock with an error hint
    /// - Already unlocked   → idempotent: unlock stays, redirect as below
    /// - Success            → unlock + redirect to /chat (chat enabled) or
    ///                        /game?tab=onderzoek (chat hidden — backend mirror
    ///                        Features:ChatEnabled of the FE FEATURE_CHAT_ENABLED
    ///                        flag; false by default)
    /// </summary>
    [HttpGet("{nfcCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> ScanNfc(string nfcCode)
    {
        // Not logged in → send to login, come back here after
        if (!(User.Identity?.IsAuthenticated ?? false))
            return LocalRedirect($"/login?returnUrl=/api/Unlock/{nfcCode}");

        var teamId = await GetTeamId();
        if (teamId == 0)
            return LocalRedirect("/?nfc=noteam");

        var locationCode = await dbContext.LocationCodes
            .Include(l => l.Location)
                .ThenInclude(l => l.Character)
            .FirstOrDefaultAsync(l => l.Code == nfcCode);

        if (locationCode == null)
            return LocalRedirect("/unlock?nfc=invalid");

        // Already unlocked — just navigate to chat
        var alreadyUnlocked = await dbContext.TeamUnlocks
            .AnyAsync(u => u.TeamId == teamId && u.LocationCodeId == locationCode.Id);

        if (!alreadyUnlocked)
        {
            // Record unlock
            dbContext.TeamUnlocks.Add(new TeamUnlockModel
            {
                TeamId = teamId,
                LocationCodeId = locationCode.Id,
                UnlockedAt = DateTime.UtcNow,
            });

            // Update progress flags
            var progress = await dbContext.TeamProgresss.FirstOrDefaultAsync(p => p.TeamId == teamId);
            if (progress == null)
            {
                progress = new TeamProgressModel { TeamId = teamId };
                dbContext.TeamProgresss.Add(progress);
            }
            progress.CanAccessChat = true;

            var totalCodes = await dbContext.LocationCodes.CountAsync();
            var teamUnlockCount = await dbContext.TeamUnlocks.CountAsync(u => u.TeamId == teamId) + 1;
            if (totalCodes > 0 && teamUnlockCount >= totalCodes)
                progress.CanSubmitTip = true;

            await dbContext.SaveChangesAsync();
        }

        // Backend mirror of the frontend FEATURE_CHAT_ENABLED flag
        // (lib/feature-flags.ts): while chat is hidden the player lands on the
        // map (Kaart tab), where the freshly unlocked location appears; with
        // chat enabled the classic /chat target is kept (the suspect found at
        // the unlocked location).
        var chatEnabled = configuration.GetValue<bool>("Features:ChatEnabled");
        return chatEnabled && locationCode.Location.CharacterId is int suspectId
            ? LocalRedirect($"/chat?character={suspectId}")
            : LocalRedirect("/game?tab=kaart");
    }

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
            .Include(l => l.Location)
                .ThenInclude(l => l.Character)
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
            locationId = locationCode.LocationId,
            locationName = locationCode.LocationName,
            // Suspect found at the unlocked location (null when none linked) —
            // used by the chat-enabled flow to name the newly available witness.
            characterName = locationCode.Location.Character?.Name,
            characterId = locationCode.Location.CharacterId,
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
                .ThenInclude(lc => lc.Location)
                    .ThenInclude(l => l.Character)
            .OrderBy(u => u.UnlockedAt)
            .Select(u => new
            {
                code = u.LocationCode.Code,
                locationId = u.LocationCode.LocationId,
                locationName = u.LocationCode.LocationName,
                // Suspect found at the unlocked location (null when none linked)
                characterName = u.LocationCode.Location.Character != null ? u.LocationCode.Location.Character.Name : null,
                characterId = u.LocationCode.Location.CharacterId,
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
