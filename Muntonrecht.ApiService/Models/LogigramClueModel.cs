namespace Muntonrecht.ApiService.Models;

/// <summary>
/// An admin-authored puzzle clue shown to every team on the logigram tab.
/// Clues are global (not per team) and must never state the solution directly.
/// </summary>
[GenerateCrud(true)]
public class LogigramClueModel
{
    public int Id { get; set; }

    /// <summary>Clue text shown to players.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Ordering in the clue list.</summary>
    public int SortOrder { get; set; }
}
