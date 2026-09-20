using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Muntonrecht.ApiService.Controllers;

[Route("/api/game")]
[ApiController]
public class GameElementController(
    UserManager<UserModel> userManager,
    MuntonrechtContext dbContext) : ControllerBase
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

    /// <summary>
    /// Returns the map-location IDs this team has unlocked: from NFC/QR
    /// location codes and from the pre-assigned location.
    /// </summary>
    private async Task<HashSet<int>> GetUnlockedLocationIdsAsync(int teamId)
    {
        var unlockedLocationIds = await dbContext.TeamUnlocks
            .Where(u => u.TeamId == teamId)
            .Include(u => u.LocationCode)
            .Select(u => u.LocationCode.LocationId)
            .Distinct()
            .ToListAsync();

        var progress = await dbContext.TeamProgresss
            .FirstOrDefaultAsync(p => p.TeamId == teamId);

        if (progress?.LocationId is int assignedLocationId)
            unlockedLocationIds.Add(assignedLocationId);

        return unlockedLocationIds.ToHashSet();
    }

    // ────────────────────────────────────────────────────────────
    // Endpoints
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the speluitleg (title, backstory, rules) for the home page.
    /// Falls back to the built-in GameDefaults when the admin has not
    /// configured custom texts in GameSettings.
    /// </summary>
    [Authorize]
    [HttpGet("Speluitleg")]
    public async Task<IActionResult> Speluitleg()
    {
        var settings = await dbContext.GameSettings.FirstOrDefaultAsync();

        return Ok(new
        {
            title = !string.IsNullOrWhiteSpace(settings?.SpeluitlegTitle)
                ? settings.SpeluitlegTitle
                : GameDefaults.SpeluitlegTitle,
            backstory = !string.IsNullOrWhiteSpace(settings?.SpeluitlegBackstory)
                ? settings.SpeluitlegBackstory
                : GameDefaults.SpeluitlegBackstory,
            rules = !string.IsNullOrWhiteSpace(settings?.SpeluitlegRules)
                ? settings.SpeluitlegRules
                : GameDefaults.SpeluitlegRules,
        });
    }

    /// <summary>
    /// Returns all locations for the map page, including the suspect found at
    /// each location and whether the current team has unlocked that suspect.
    /// Unlocked teams can start a chat; locked locations point to /unlock.
    /// For unlocked locations the investigation content (interview transcript or
    /// search picture) is included as parsed JSON; locked locations always get
    /// content = null so nothing can be leaked early.
    /// </summary>
    [Authorize]
    [HttpGet("Locations")]
    public async Task<IActionResult> Locations()
    {
        var user = await GetCurrentUser();

        var unlockedLocationIds = user is { TeamId: > 0 }
            ? await GetUnlockedLocationIdsAsync(user.TeamId)
            : [];

        var locations = await dbContext.Locations
            .Include(l => l.Character)
            .OrderBy(l => l.Name)
            .ToListAsync();

        var result = locations.Select(l =>
        {
            var isUnlocked = unlockedLocationIds.Contains(l.Id);

            // Content is only ever parsed for unlocked locations.
            JsonNode? content = null;
            if (isUnlocked && !string.IsNullOrWhiteSpace(l.ContentJson))
            {
                try
                {
                    content = JsonNode.Parse(l.ContentJson);
                }
                catch (JsonException)
                {
                    // Malformed admin-authored JSON is treated as "no content".
                    content = null;
                }
            }

            return new
            {
                id = l.Id,
                name = l.Name,
                description = l.Description,
                latitude = l.Latitude,
                longitude = l.Longitude,
                characterId = l.CharacterId,
                characterName = l.Character?.Name,
                characterAvatarUrl = l.Character?.AvatarUrl,
                isUnlocked,
                contentType = l.ContentType,
                content,
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// Returns the current game state for the authenticated player's team:
    /// chat/tip access flags, unlock progress, playtest info and the
    /// intro/rules seen flags driving the post-login flow.
    /// </summary>
    [Authorize]
    [HttpGet("Status")]
    public async Task<IActionResult> Status()
    {
        var user = await GetCurrentUser();

        if (user == null || user.TeamId == 0)
            return Ok(new
            {
                canAccessChat = false,
                canSubmitTip = false,
                unlockedLocations = 0,
                totalLocations = 0,
                isPlaytest = false,
                barName = (string?)null,
                introSeen = false,
                rulesSeen = false,
            });

        var team = await dbContext.Teams.FindAsync(user.TeamId);
        var progress = await GetOrCreateTeamProgress(user.TeamId);

        var visits = await LocationVisitProgress.GetAsync(dbContext, user.TeamId);

        return Ok(new
        {
            canAccessChat = progress.CanAccessChat,
            canSubmitTip = visits.Total > 0 && visits.Visited == visits.Total,
            unlockedLocations = visits.Visited,
            totalLocations = visits.Total,
            isPlaytest = team?.IsPlaytest ?? false,
            barName = team?.BarName,
            introSeen = progress.IntroSeenAt != null,
            rulesSeen = progress.RulesSeenAt != null,
        });
    }

    /// <summary>
    /// Returns the suspects found at the locations the current team has unlocked
    /// via location codes or the pre-assigned location. Used by the chat page to
    /// only show accessible suspects.
    /// </summary>
    [Authorize]
    [HttpGet("UnlockedCharacters")]
    public async Task<IActionResult> UnlockedCharacters()
    {
        var user = await GetCurrentUser();
        if (user == null || user.TeamId == 0)
            return Ok(new List<object>());

        var unlockedLocationIds = await GetUnlockedLocationIdsAsync(user.TeamId);

        if (!unlockedLocationIds.Any())
            return Ok(new List<object>());

        var unlockedCharacterIds = await dbContext.Locations
            .Where(l => unlockedLocationIds.Contains(l.Id) && l.CharacterId != null)
            .Select(l => l.CharacterId!.Value)
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

    /// <summary>
    /// Returns the pre-assigned location for the current team.
    /// </summary>
    [Authorize]
    [HttpGet("AssignedLocation")]
    public async Task<IActionResult> AssignedLocation()
    {
        var user = await GetCurrentUser();
        if (user?.TeamId > 0)
        {
            var progress = await dbContext.TeamProgresss
                .Include(p => p.Location)
                .FirstOrDefaultAsync(p => p.TeamId == user.TeamId);

            if (progress?.Location != null)
            {
                return Ok(new
                {
                    location = progress.Location.Name,
                    description = progress.Location.Description,
                    characterId = progress.Location.CharacterId,
                    latitude = progress.Location.Latitude,
                    longitude = progress.Location.Longitude,
                });
            }
        }

        return Ok(new { location = (string?)null, characterId = (int?)null });
    }

    /// <summary>
    /// Returns the intro (detective telegram) shown after login.
    /// Falls back to GameDefaults when the admin has not configured custom texts.
    /// </summary>
    [Authorize]
    [HttpGet("Intro")]
    public async Task<IActionResult> Intro()
    {
        var settings = await dbContext.GameSettings.FirstOrDefaultAsync();

        return Ok(new
        {
            title = !string.IsNullOrWhiteSpace(settings?.IntroTitle)
                ? settings.IntroTitle
                : GameDefaults.IntroTitle,
            body = !string.IsNullOrWhiteSpace(settings?.IntroBody)
                ? settings.IntroBody
                : GameDefaults.IntroBody,
        });
    }

    /// <summary>
    /// Returns the rules shown after the intro.
    /// Falls back to GameDefaults when the admin has not configured custom texts.
    /// </summary>
    [Authorize]
    [HttpGet("Rules")]
    public async Task<IActionResult> Rules()
    {
        var settings = await dbContext.GameSettings.FirstOrDefaultAsync();

        return Ok(new
        {
            title = !string.IsNullOrWhiteSpace(settings?.RulesTitle)
                ? settings.RulesTitle
                : GameDefaults.RulesTitle,
            body = !string.IsNullOrWhiteSpace(settings?.RulesBody)
                ? settings.RulesBody
                : GameDefaults.RulesBody,
        });
    }

    /// <summary>
    /// Marks the intro (telegram) as seen for the current team (idempotent).
    /// Drives the post-login flow /intro → /rules → /game.
    /// </summary>
    [Authorize]
    [HttpPost("MarkIntroSeen")]
    public async Task<IActionResult> MarkIntroSeen()
    {
        var progress = await GetCurrentTeamProgressOrNull();
        if (progress == null)
            return BadRequest("Je bent niet aan een team gekoppeld.");

        progress.IntroSeenAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return Ok(new { introSeen = true });
    }

    /// <summary>
    /// Marks the rules as seen for the current team (idempotent).
    /// </summary>
    [Authorize]
    [HttpPost("MarkRulesSeen")]
    public async Task<IActionResult> MarkRulesSeen()
    {
        var progress = await GetCurrentTeamProgressOrNull();
        if (progress == null)
            return BadRequest("Je bent niet aan een team gekoppeld.");

        progress.RulesSeenAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return Ok(new { rulesSeen = true });
    }

    /// <summary>
    /// Returns everything the logigram tab needs: categories, entries, clues and
    /// the current team's marks. The solution is never included — it lives only
    /// in GameSettings and is evaluated server-side by the tip endpoint.
    /// </summary>
    [Authorize]
    [HttpGet("Logigram")]
    public async Task<IActionResult> Logigram()
    {
        var user = await GetCurrentUser();

        // The grid is derived from the game content (suspects, weapons,
        // locations); sync before serving so the board always reflects the
        // current characters/weapons/locations. Never fail the player request
        // because of the sync — fall back to the previously synced grid.
        try
        {
            await LogigramSyncManager.SyncFromGameElementsAsync(dbContext);
        }
        catch (Exception)
        {
            // Intentionally swallowed: serve the previously synced grid.
        }

        var categories = await dbContext.LogigramCategorys
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .ToListAsync();
        var entries = await dbContext.LogigramEntrys
            .OrderBy(e => e.SortOrder).ThenBy(e => e.Id)
            .ToListAsync();
        var clues = await dbContext.LogigramClues
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .ToListAsync();

        var marks = user is { TeamId: > 0 }
            ? await dbContext.TeamLogigramMarks
                .Where(m => m.TeamId == user.TeamId)
                .ToListAsync()
            : [];

        return Ok(new
        {
            categories = categories.Select(c => new
            {
                id = c.Id,
                key = c.Key,
                name = c.Name,
                sortOrder = c.SortOrder,
            }),
            entries = entries.Select(e => new
            {
                id = e.Id,
                categoryId = e.LogigramCategoryId,
                name = e.Name,
                imageUrl = e.ImageUrl,
                sortOrder = e.SortOrder,
            }),
            clues = clues.Select(c => new
            {
                id = c.Id,
                text = c.Text,
                sortOrder = c.SortOrder,
            }),
            marks = marks.Select(m => new
            {
                entryAId = m.LogigramEntryId,
                // null → per-entry mark (header conclusion); set → unordered
                // pair mark (grid cell), normalized entryA = min, entryB = max.
                entryBId = m.LogigramEntryBId,
                mark = m.Mark,
            }),
        });
    }

    /// <summary>
    /// Replaces the current team's logigram marks (bulk save, last write wins
    /// per (entryA, COALESCE(entryB, 0))). A mark with entryBId set is an
    /// unordered pair mark: the API normalizes entryA = min(idA, idB),
    /// entryB = max(idA, idB). Marks with value "none" are not persisted.
    /// </summary>
    [Authorize]
    [HttpPut("Logigram/Marks")]
    public async Task<IActionResult> SaveLogigramMarks([FromBody] LogigramMarksDTO dto)
    {
        var user = await GetCurrentUser();
        if (user == null || user.TeamId == 0)
            return BadRequest("Je bent niet aan een team gekoppeld.");

        var allowedMarks = new HashSet<string> { "none", "cross", "check" };

        // Normalize + validate; last write wins per (entryA, COALESCE(entryB, 0)).
        var normalized = new List<(int EntryAId, int? EntryBId, string Mark)>();
        foreach (var m in dto.Marks ?? [])
        {
            var mark = m.Mark?.Trim().ToLowerInvariant() ?? "none";
            if (!allowedMarks.Contains(mark))
                return BadRequest("Onbekende markering. Gebruik none, cross of check.");

            var a = m.EntryAId;
            var b = m.EntryBId;
            if (a <= 0)
                continue; // ignore malformed rows
            if (b == a)
                b = null; // an "entry with itself" pair collapses to a per-entry mark
            if (b.HasValue && b.Value <= 0)
                continue;
            if (b.HasValue && b.Value < a)
                (a, b) = (b.Value, a); // unordered pair: entryA = min, entryB = max

            // Last write wins: drop an earlier request for the same cell/header.
            normalized.RemoveAll(x => x.EntryAId == a && (x.EntryBId ?? 0) == (b ?? 0));
            normalized.Add((a, b, mark));
        }

        // All referenced entries (both sides of a pair) must exist.
        var entryIds = normalized
            .SelectMany(m => m.EntryBId.HasValue
                ? new[] { m.EntryAId, m.EntryBId.Value }
                : new[] { m.EntryAId })
            .Distinct()
            .ToList();

        if (entryIds.Count > 0)
        {
            var existingCount = await dbContext.LogigramEntrys
                .CountAsync(e => entryIds.Contains(e.Id));
            if (existingCount < entryIds.Count)
                return BadRequest("Eén of meer logigram-items bestaan niet.");
        }

        // Upsert (update in place / add new / remove missing) so a bulk replace
        // never trips the unique (team, entryA, COALESCE(entryB, 0)) index.
        var currentMarks = await dbContext.TeamLogigramMarks
            .Where(m => m.TeamId == user.TeamId)
            .ToListAsync();
        var currentByKey = currentMarks.ToDictionary(m => (m.LogigramEntryId, EntryB: m.LogigramEntryBId ?? 0));

        foreach (var (entryAId, entryBId, mark) in normalized)
        {
            if (currentByKey.Remove((entryAId, entryBId ?? 0), out var existing))
            {
                if (mark == "none")
                    dbContext.TeamLogigramMarks.Remove(existing);
                else
                    existing.Mark = mark;
            }
            else if (mark != "none")
            {
                dbContext.TeamLogigramMarks.Add(new TeamLogigramMarkModel
                {
                    TeamId = user.TeamId,
                    LogigramEntryId = entryAId,
                    LogigramEntryBId = entryBId,
                    Mark = mark,
                });
            }
        }

        // Marks no longer in the payload are removed (full replace).
        foreach (var orphan in currentByKey.Values)
            dbContext.TeamLogigramMarks.Remove(orphan);

        await dbContext.SaveChangesAsync();

        return Ok(new { saved = true });
    }

    /// <summary>
    /// Returns the TeamProgress for the current user's team, creating it if
    /// needed; null when the user is not linked to a team.
    /// </summary>
    private async Task<TeamProgressModel?> GetCurrentTeamProgressOrNull()
    {
        var user = await GetCurrentUser();
        if (user == null || user.TeamId == 0)
            return null;

        return await GetOrCreateTeamProgress(user.TeamId);
    }

    // ────────────────────────────────────────────────────────────
    // DTOs
    // ────────────────────────────────────────────────────────────
    public class LogigramMarksDTO
    {
        public List<LogigramMarkDTO>? Marks { get; set; }
    }

    public class LogigramMarkDTO
    {
        /// <summary>First entry of the mark (per-entry mark: the entry itself).</summary>
        public int EntryAId { get; set; }

        /// <summary>Second entry of an unordered pair mark; null for per-entry marks.</summary>
        public int? EntryBId { get; set; }

        /// <summary>"none" | "cross" | "check".</summary>
        public string Mark { get; set; } = "none";
    }
}
