namespace Muntonrecht.ApiService.Models;

/// <summary>
/// A physical location code that players find at a real-world location in Zutphen.
/// Entering the code unlocks the linked map location (its dossier on the
/// Onderzoek tab and, when chat is enabled, the suspect found there).
/// </summary>
[GenerateCrud(true)]
public class LocationCodeModel
{
    public int Id { get; set; }

    /// <summary>
    /// Code used for both manual entry and NFC scanning.
    /// For NFC tags, set this to a GUID — the tag URL will be /api/Unlock/{Code}.
    /// For manual entry, use a short human-readable code (e.g. "KERK2026").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable name of the location (e.g. "De Kerk")</summary>
    public string LocationName { get; set; } = string.Empty;

    /// <summary>Message shown to the player when this code is successfully entered</summary>
    public string UnlockMessage { get; set; } = string.Empty;

    /// <summary>The map location that gets unlocked when this code is entered</summary>
    public int LocationId { get; set; }
    public LocationModel Location { get; set; } = null!;
    public List<TeamUnlockModel> TeamUnlocks { get; set; } = [];
}
