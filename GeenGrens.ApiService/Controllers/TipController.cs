using System.Security.Claims;

namespace GeenGrens.ApiService.Controllers;

/// <summary>
/// Handles anonymous tip submissions to the police.
/// The correct answer is evaluated server-side so the client never sees it.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TipController(
    UserManager<UserModel> userManager,
    GeenGrensContext dbContext) : ControllerBase
{
    // ─────────────────────────────────────────────────────────────────────
    // Game answers – kept server-side so they are never exposed to players
    // ─────────────────────────────────────────────────────────────────────
    private const string CorrectKiller = "cafe-eigenaar";

    private static readonly string[] CorrectMotiveKeywords =
    [
        "aandacht", "zaak", "failliet", "failliete", "verliep", "publiciteit",
    ];

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
    /// Submit the team's final accusation. Can only be submitted once.
    /// Returns whether the team got it right.
    /// </summary>
    [HttpPost("Submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitTipDTO dto)
    {
        var teamId = await GetTeamId();
        if (teamId == 0)
            return BadRequest("Je bent niet aan een team gekoppeld.");

        var progress = await dbContext.TeamProgresss
            .FirstOrDefaultAsync(p => p.TeamId == teamId);

        if (progress == null || !progress.CanSubmitTip)
            return Forbid();

        // Return cached result if already submitted
        if (progress.TipSubmitted)
            return Ok(new
            {
                alreadySubmitted = true,
                isCorrect = progress.TipIsCorrect,
                suspectId = progress.TipSuspectId,
            });

        if (string.IsNullOrWhiteSpace(dto.SuspectId) || string.IsNullOrWhiteSpace(dto.Motive))
            return BadRequest("Verdachte en motief zijn verplicht.");

        var killerCorrect = dto.SuspectId.Trim().ToLowerInvariant() == CorrectKiller;
        var motiveText = dto.Motive.ToLowerInvariant();
        var motiveCorrect = CorrectMotiveKeywords.Any(k => motiveText.Contains(k));
        var isCorrect = killerCorrect && motiveCorrect;

        progress.TipSubmitted = true;
        progress.TipSuspectId = dto.SuspectId;
        progress.TipMotive = dto.Motive;
        progress.TipIsCorrect = isCorrect;

        await dbContext.SaveChangesAsync();

        return Ok(new { alreadySubmitted = false, isCorrect });
    }

    // ────────────────────────────────────────────────────────────
    // DTOs
    // ────────────────────────────────────────────────────────────
    public class SubmitTipDTO
    {
        public string SuspectId { get; set; } = string.Empty;
        public string Motive { get; set; } = string.Empty;
    }
}
