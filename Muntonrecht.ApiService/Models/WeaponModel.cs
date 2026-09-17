namespace Muntonrecht.ApiService.Models;

/// <summary>
/// A murder weapon that players must deduce through AI chat conversations.
/// Each weapon has a stop keyword that, when detected in an assistant reply,
/// signals that the weapon has been discovered.
/// </summary>
[GenerateCrud(true)]
public class WeaponModel
{
    public int Id { get; set; }

    /// <summary>Name of the weapon (e.g. "Dolk", "Pistool", "Vergif")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of the weapon</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Keyword(s) to detect in assistant replies that confirm the weapon has been discovered.</summary>
    public string? StopKeywordWeapon { get; set; }

    public List<TeamWeaponModel> TeamWeapons { get; set; } = [];
    public List<TeamProgressModel> TeamProgresss { get; set; } = [];
}
