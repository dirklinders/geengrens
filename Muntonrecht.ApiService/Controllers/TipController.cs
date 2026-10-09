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

        var visits = await LocationVisitProgress.GetAsync(dbContext, teamId);
        if (progress == null || visits.Total == 0 || visits.Visited != visits.Total)
            return Forbid();

        // A final accusation is strictly one-shot. Do not return the verdict
        // here: players receive only a receipt, and admins see the result.
        if (progress.TipSubmitted)
            return Conflict("Er is al een definitieve aanklacht voor dit team ingediend.");

        var settings = await dbContext.GameSettings.FirstOrDefaultAsync();
        if (settings == null || settings.MurdererCharacterId <= 0 ||
            settings.MurderWeaponId <= 0 || settings.MurderLocationId <= 0)
            return BadRequest("Deze ronde is nog niet volledig geconfigureerd. Neem contact op met de spelleider.");

        if (dto.CharacterId is null or <= 0 || dto.WeaponId is null or <= 0 || dto.LocationId is null or <= 0)
            return BadRequest("Verdachte, wapen en locatie zijn verplicht.");

        var isCorrect = dto.CharacterId == settings.MurdererCharacterId
            && dto.WeaponId == settings.MurderWeaponId
            && dto.LocationId == settings.MurderLocationId;
        var accusedCharacter = await dbContext.Characters.FindAsync(dto.CharacterId.Value);

        progress.TipSubmitted = true;
        progress.TipSuspectId = dto.CharacterId.Value.ToString();
        progress.TipSuspectDisplayName = accusedCharacter?.Name == "?"
            ? progress.UnknownSuspectName
            : null;
        progress.TipWeaponId = dto.WeaponId;
        progress.TipLocationId = dto.LocationId;
        progress.TipIsCorrect = isCorrect;

        await dbContext.SaveChangesAsync();

        return Ok(new { submitted = true });
    }

    /// <summary>
    /// Imports NFC/QR location codes. Each row is matched to an existing map
    /// location by its name, so the CSV does not depend on database IDs.
    /// Expected columns: Code,Locatienaam,UnlockMessage.
    /// </summary>
    [HttpPost("UploadLocations")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadLocations(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Geen bestand ontvangen.");

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Alleen CSV-bestanden worden geaccepteerd.");

        var locationCodes = new List<LocationCodeModel>();

        using var reader = new StreamReader(file.OpenReadStream());
        var header = await reader.ReadLineAsync();
        if (header == null)
            return BadRequest("Leeg CSV-bestand.");

        var headers = header.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
        if (!headers.Contains("code") || !headers.Contains("locatienaam"))
            return BadRequest("CSV moet minimaal de kolommen Code en Locatienaam bevatten.");

        var locationsByName = (await dbContext.Locations.ToListAsync())
            .ToDictionary(location => location.Name, StringComparer.OrdinalIgnoreCase);
        var importedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int lineNum = 0;
        while (!reader.EndOfStream)
        {
            lineNum++;
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = ParseCsvLine(line);
            var code = GetValue(values, headers, "code", lineNum).Trim().ToUpperInvariant();
            var locationName = GetValue(values, headers, "locatienaam", lineNum).Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(locationName))
                return BadRequest($"Regel {lineNum}: Code en Locatienaam zijn verplicht.");
            if (!locationsByName.TryGetValue(locationName, out var location))
                return BadRequest($"Regel {lineNum}: locatie '{locationName}' bestaat niet.");
            if (!importedCodes.Add(code))
                return BadRequest($"Regel {lineNum}: code '{code}' komt meerdere keren voor in het bestand.");

            locationCodes.Add(new LocationCodeModel
            {
                Code = code,
                LocationName = locationName,
                UnlockMessage = GetValue(values, headers, "unlockmessage", lineNum),
                LocationId = location.Id,
            });
        }

        var existingCodes = await dbContext.LocationCodes
            .Select(locationCode => locationCode.Code)
            .ToListAsync();
        var duplicate = existingCodes.FirstOrDefault(code => importedCodes.Contains(code));
        if (duplicate != null)
            return Conflict($"Code '{duplicate}' bestaat al.");

        dbContext.LocationCodes.AddRange(locationCodes);
        await dbContext.SaveChangesAsync();

        return Ok(new { count = locationCodes.Count, message = $" {locationCodes.Count} locatiecodes geïmporteerd." });
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
