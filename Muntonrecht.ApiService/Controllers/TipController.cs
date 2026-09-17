using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Muntonrecht.ApiService.Controllers;

/// <summary>
/// Handles anonymous tip submissions to the police.
/// The correct answer is evaluated server-side so the client never sees it.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TipController(
    UserManager<UserModel> userManager,
    MuntonrechtContext dbContext) : ControllerBase
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
    /// Submit the team's final Cluedo accusation: suspect + weapon + location.
    /// Can only be submitted once. The correct answer is read from the
    /// GameSettings row so it is never exposed to players.
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
            });

        var settings = await dbContext.GameSettings.FirstOrDefaultAsync();
        if (settings == null || settings.MurdererCharacterId <= 0 ||
            settings.MurderWeaponId <= 0 || settings.MurderLocationId <= 0)
            return BadRequest("Deze ronde is nog niet volledig geconfigureerd. Neem contact op met de spelleider.");

        if (dto.CharacterId is null or <= 0 || dto.WeaponId is null or <= 0 || dto.LocationId is null or <= 0)
            return BadRequest("Verdachte, wapen en locatie zijn verplicht.");

        var isCorrect = dto.CharacterId == settings.MurdererCharacterId
            && dto.WeaponId == settings.MurderWeaponId
            && dto.LocationId == settings.MurderLocationId;

        progress.TipSubmitted = true;
        progress.TipSuspectId = dto.CharacterId.Value.ToString();
        progress.TipWeaponId = dto.WeaponId;
        progress.TipLocationId = dto.LocationId;
        progress.TipIsCorrect = isCorrect;

        await dbContext.SaveChangesAsync();

        return Ok(new { alreadySubmitted = false, isCorrect });
    }

    /// <summary>
    /// Upload a CSV file with location coordinates.
    /// Expected columns: Name,Description,Latitude,Longitude,CharacterId
    /// </summary>
    [HttpPost("UploadLocations")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadLocations(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Geen bestand ontvangen.");

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Alleen CSV-bestanden worden geaccepteerd.");

        var locations = new List<LocationModel>();

        using var reader = new StreamReader(file.OpenReadStream());
        var header = await reader.ReadLineAsync();
        if (header == null)
            return BadRequest("Leeg CSV-bestand.");

        var headers = header.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();

        int lineNum = 0;
        while (!reader.EndOfStream)
        {
            lineNum++;
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = ParseCsvLine(line);
            if (values.Length < headers.Length) continue;

            var parsedCharacterId = ParseInt(GetValue(values, headers, "characterid", lineNum), lineNum, "characterId");

            var location = new LocationModel
            {
                Name = GetValue(values, headers, "name", lineNum),
                Description = GetValue(values, headers, "description", lineNum),
                Latitude = ParseDouble(GetValue(values, headers, "latitude", lineNum), lineNum, "latitude"),
                Longitude = ParseDouble(GetValue(values, headers, "longitude", lineNum), lineNum, "longitude"),
                // Missing/0 means "no suspect" — store NULL so the FK stays satisfied.
                CharacterId = parsedCharacterId > 0 ? parsedCharacterId : null,
            };
            locations.Add(location);
        }

        dbContext.Locations.AddRange(locations);
        await dbContext.SaveChangesAsync();

        return Ok(new { count = locations.Count, message = $" {locations.Count} locaties geïmporteerd." });
    }

    // ────────────────────────────────────────────────────────────
    // DTOs
    // ────────────────────────────────────────────────────────────
    public class SubmitTipDTO
    {
        public int? CharacterId { get; set; }
        public int? WeaponId { get; set; }
        public int? LocationId { get; set; }
    }

    // ────────────────────────────────────────────────────────────
    // CSV helpers
    // ────────────────────────────────────────────────────────────

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim());
        return result.ToArray();
    }

    private static string GetValue(string[] values, string[] headers, string headerName, int lineNum)
    {
        var idx = Array.FindIndex(headers, h => h == headerName);
        if (idx < 0 || idx >= values.Length) return string.Empty;
        return values[idx];
    }

    private static double ParseDouble(string value, int lineNum, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        if (double.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;
        throw new InvalidOperationException($"Regel {lineNum}: ongeldige waarde voor '{fieldName}': '{value}'");
    }

    private static int ParseInt(string value, int lineNum, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        if (int.TryParse(value, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;
        throw new InvalidOperationException($"Regel {lineNum}: ongeldige waarde voor '{fieldName}': '{value}'");
    }
}
