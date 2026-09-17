namespace Muntonrecht.ApiService.Models;

/// <summary>
/// A logigram dimension/category, e.g. suspects, weapons or locations.
/// The true solution is NOT stored here — it lives in GameSettings;
/// admins keep entry names consistent with the configured entities.
/// </summary>
[GenerateCrud(true)]
public class LogigramCategoryModel
{
    public int Id { get; set; }

    /// <summary>Stable key: "suspect" | "weapon" | "location".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display label, e.g. "Verdachten".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ordering in the player UI.</summary>
    public int SortOrder { get; set; }

    public List<LogigramEntryModel> LogigramEntrys { get; set; } = [];
}
