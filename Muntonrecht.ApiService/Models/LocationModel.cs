namespace Muntonrecht.ApiService.Models;

/// <summary>
/// A location pre-assigned to a team. Players start at this location
/// and must gather clues by talking to suspects.
/// Coordinates allow CSV upload for bulk location management.
/// </summary>
[GenerateCrud(true)]
public class LocationModel
{
    public int Id { get; set; }

    /// <summary>Human-readable name of the location (e.g. "De Stadsmarkt")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of the location shown to players</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Content discriminator for the investigation tab: '' (plain description only),
    /// 'interview' (redacted police transcript) or 'search_picture' (photo with hotspots).
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Admin-authored content as JSON, shaped by ContentType:
    /// interview → { "header", "meta", "lines": [{ "speaker", "text" }] } where text may
    /// contain [[...]] spans that render as redacted (blacked-out) bars;
    /// search_picture → { "note", "image", "hotspots": [{ "id", "x", "y", "label", "detail" }] }
    /// with x/y as percentages of the image size.
    /// Only exposed to players via /api/game/Locations when the location is unlocked.
    /// </summary>
    public string ContentJson { get; set; } = string.Empty;

    /// <summary>Latitude coordinate</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude coordinate</summary>
    public double Longitude { get; set; }

    /// <summary>
    /// The character/suspect available at this location. Nullable: a location may
    /// exist without a linked suspect (admin option "— Geen verdachte gekoppeld —").
    /// EF then inserts NULL, which satisfies the "Locations_CharacterId_fkey" FK.
    /// </summary>
    public int? CharacterId { get; set; }
    public CharacterModel? Character { get; set; }

    /// <summary>Location codes (NFC/QR) that unlock this location.</summary>
    public List<LocationCodeModel> LocationCodes { get; set; } = [];

    public List<TeamProgressModel> TeamProgresss { get; set; } = [];
}
