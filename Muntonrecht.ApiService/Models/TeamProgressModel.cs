namespace Muntonrecht.ApiService.Models;

/// <summary>
/// Tracks a team's progress through the Cluedo game.
/// One record per team.
/// </summary>
[GenerateCrud(true)]
public class TeamProgressModel
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    public TeamModel Team { get; set; } = null!;

    /// <summary>Whether the team can access the suspect interrogation chat</summary>
    public bool CanAccessChat { get; set; }

    /// <summary>Whether the team can submit an anonymous tip to the police</summary>
    public bool CanSubmitTip { get; set; }

    /// <summary>Whether the team has already submitted their final tip</summary>
    public bool TipSubmitted { get; set; }

    /// <summary>The suspect ID the team accused (CharacterModel Id as string)</summary>
    public string? TipSuspectId { get; set; }

    /// <summary>Whether their tip was correct (set after submission)</summary>
    public bool? TipIsCorrect { get; set; }

    /// <summary>The location the team is assigned to (pre-assigned, not NFC discovered)</summary>
    public int? LocationId { get; set; }
    public LocationModel? Location { get; set; }

    /// <summary>The weapon the team has deduced through chat</summary>
    public int? WeaponId { get; set; }
    public WeaponModel? Weapon { get; set; }

    /// <summary>The location ID the team accused in their final tip</summary>
    public int? TipLocationId { get; set; }

    /// <summary>The weapon ID the team accused in their final tip</summary>
    public int? TipWeaponId { get; set; }

    /// <summary>UTC timestamp of when the team viewed the intro (telegram). Null = not seen yet.</summary>
    public DateTime? IntroSeenAt { get; set; }

    /// <summary>UTC timestamp of when the team viewed the rules screen. Null = not seen yet.</summary>
    public DateTime? RulesSeenAt { get; set; }
}
