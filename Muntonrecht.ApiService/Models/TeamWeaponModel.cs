namespace Muntonrecht.ApiService.Models;

/// <summary>
/// Records that a specific team has discovered a specific weapon through chat.
/// </summary>
[GenerateCrud(true)]
public class TeamWeaponModel
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public TeamModel Team { get; set; } = null!;

    public int WeaponId { get; set; }
    public WeaponModel Weapon { get; set; } = null!;

    /// <summary>UTC timestamp of when the weapon was discovered</summary>
    public DateTime DiscoveredAt { get; set; }
}
