namespace GeenGrens.ApiService.Models;

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

    /// <summary>Whether the team has entered the correct notebook password</summary>
    public bool IsNotebookUnlocked { get; set; }

    /// <summary>Whether the team can access the suspect interrogation chat</summary>
    public bool CanAccessChat { get; set; }

    /// <summary>Whether the team can submit an anonymous tip to the police</summary>
    public bool CanSubmitTip { get; set; }

    /// <summary>Whether the team has already submitted their final tip</summary>
    public bool TipSubmitted { get; set; }

    /// <summary>The suspect ID the team accused</summary>
    public string? TipSuspectId { get; set; }

    /// <summary>The motive the team provided</summary>
    public string? TipMotive { get; set; }

    /// <summary>Whether their tip was correct (set after submission)</summary>
    public bool? TipIsCorrect { get; set; }
}
