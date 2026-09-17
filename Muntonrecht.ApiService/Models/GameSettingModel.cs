namespace Muntonrecht.ApiService.Models;

/// <summary>
/// Single-row game configuration: the Cluedo solution (who / what / where)
/// and the editable speluitleg shown on the home page.
/// IDs default to 0 = "not configured yet".
/// NOTE: solution fields must never be exposed through player-facing endpoints.
/// </summary>
[GenerateCrud(true)]
public class GameSettingModel
{
    public int Id { get; set; }

    /// <summary>The suspect (CharacterModel) who committed the murder. 0 = not configured.</summary>
    public int MurdererCharacterId { get; set; }

    /// <summary>The weapon used in the murder. 0 = not configured.</summary>
    public int MurderWeaponId { get; set; }

    /// <summary>
    /// The location where the murder took place. The body was moved afterwards,
    /// so this is NOT necessarily the location where the murderer is found.
    /// 0 = not configured.
    /// </summary>
    public int MurderLocationId { get; set; }

    /// <summary>Title of the speluitleg page. Empty = use GameDefaults.</summary>
    public string SpeluitlegTitle { get; set; } = string.Empty;

    /// <summary>Backstory of the murder shown on the home page. Empty = use GameDefaults.</summary>
    public string SpeluitlegBackstory { get; set; } = string.Empty;

    /// <summary>How-to-play instructions shown on the home page. Empty = use GameDefaults.</summary>
    public string SpeluitlegRules { get; set; } = string.Empty;

    /// <summary>Title of the intro (detective telegram) shown after login. Empty = use GameDefaults.</summary>
    public string IntroTitle { get; set; } = string.Empty;

    /// <summary>Body of the intro (detective telegram). Empty = use GameDefaults.</summary>
    public string IntroBody { get; set; } = string.Empty;

    /// <summary>Title of the rules screen shown after the intro. Empty = use GameDefaults.</summary>
    public string RulesTitle { get; set; } = string.Empty;

    /// <summary>Body of the rules screen. Empty = use GameDefaults.</summary>
    public string RulesBody { get; set; } = string.Empty;
}
