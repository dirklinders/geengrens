namespace GeenGrens.ApiService.Models;

/// <summary>
/// Records that a specific team has successfully entered a specific location code.
/// Prevents teams from entering the same code twice.
/// </summary>
[GenerateCrud(true)]
public class TeamUnlockModel
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public TeamModel Team { get; set; } = null!;

    public int LocationCodeId { get; set; }
    public LocationCodeModel LocationCode { get; set; } = null!;

    /// <summary>UTC timestamp of when the code was entered</summary>
    public DateTime UnlockedAt { get; set; }
}
