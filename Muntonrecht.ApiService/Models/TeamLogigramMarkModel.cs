namespace Muntonrecht.ApiService.Models;

/// <summary>
/// A per-team persisted logigram mark: "none" | "cross" | "check".
/// Marks are team-scoped (not per user) and saved last-write-wins via
/// PUT /api/game/Logigram/Marks. One row per (team, entryA, COALESCE(entryB, 0)):
/// - LogigramEntryBId == null     → per-entry mark (row/column conclusion);
/// - LogigramEntryBId set (both)  → unordered pair mark for one grid cell,
///   normalized by the API so entryA = min(idA, idB), entryB = max(idA, idB).
/// LogigramEntryBId deliberately has no EF navigation property (no
/// LogigramEntryBModel exists, so the CrudGenerator ignores it); the DB-level
/// FK with ON DELETE CASCADE is created by manual_012_logigram_pair_marks.sql.
/// </summary>
[GenerateCrud(true)]
public class TeamLogigramMarkModel
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public TeamModel Team { get; set; } = null!;

    /// <summary>The logigram entry this mark applies to (pair mark: the lower entry id).</summary>
    public int LogigramEntryId { get; set; }
    public LogigramEntryModel LogigramEntry { get; set; } = null!;

    /// <summary>
    /// Optional second entry of an unordered pair mark; null for per-entry marks.
    /// </summary>
    public int? LogigramEntryBId { get; set; }

    /// <summary>"none" | "cross" | "check".</summary>
    public string Mark { get; set; } = "none";
}
