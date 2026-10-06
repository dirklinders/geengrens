namespace Muntonrecht.ApiService.Models;

/// <summary>
/// A hotspot discovered in a search picture by a team. Discoveries belong to
/// the whole team rather than an individual player, so they survive reloads
/// and are immediately visible to team mates.
/// </summary>
[GenerateCrud(true)]
public class TeamSearchPictureRevealModel
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public TeamModel Team { get; set; } = null!;

    public int LocationId { get; set; }
    public LocationModel Location { get; set; } = null!;

    /// <summary>The stable hotspot id from the location's search-picture JSON.</summary>
    public string HotspotId { get; set; } = string.Empty;

    public DateTime RevealedAt { get; set; } = DateTime.UtcNow;
}
