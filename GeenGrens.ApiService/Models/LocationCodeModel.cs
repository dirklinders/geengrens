namespace GeenGrens.ApiService.Models;

/// <summary>
/// A physical location code that players find at a real-world location in Baarle-Nassau.
/// Entering the code unlocks a suspect character for interrogation.
/// </summary>
[GenerateCrud(true)]
public class LocationCodeModel
{
    public int Id { get; set; }

    /// <summary>Code players must enter (e.g. "KERK2026")</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable name of the location (e.g. "De Kerk")</summary>
    public string LocationName { get; set; } = string.Empty;

    /// <summary>Message shown to the player when this code is successfully entered</summary>
    public string UnlockMessage { get; set; } = string.Empty;

    /// <summary>The character/suspect that gets unlocked when this code is entered</summary>
    public int CharacterId { get; set; }
    public CharacterModel Character { get; set; } = null!;
    public List<TeamUnlockModel> TeamUnlocks { get; set; } = [];
}
