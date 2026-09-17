namespace Muntonrecht.ApiService.Models;

/// <summary>
/// An entry (card) within a logigram category, e.g. "De barman" or "Dolk".
/// The true solution stays in GameSettings (MurdererCharacterId / MurderWeaponId /
/// MurderLocationId) — entries are deliberately not linked to those IDs.
/// </summary>
[GenerateCrud(true)]
public class LogigramEntryModel
{
    public int Id { get; set; }

    public int LogigramCategoryId { get; set; }
    public LogigramCategoryModel LogigramCategory { get; set; } = null!;

    /// <summary>
    /// Source entity id when this entry was auto-synced from the game content
    /// (CharacterModel.Id / WeaponModel.Id / LocationModel.Id — the category key
    /// determines which). Null for legacy/manual entries.
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>Display name, e.g. "De barman" or "Dolk".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional image (e.g. avatar) shown in the grid header.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Ordering within the category.</summary>
    public int SortOrder { get; set; }

    public List<TeamLogigramMarkModel> TeamLogigramMarks { get; set; } = [];
}
