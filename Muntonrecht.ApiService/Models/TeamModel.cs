namespace Muntonrecht.ApiService.Models;

[GenerateCrud(true)]
public class TeamModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// When true, this team gets access to the digital playtest notebook in the game UI.
    /// </summary>
    public bool IsPlaytest { get; set; }
    /// <summary>
    /// The name of the bar used in this team's story. Replaces {BarNaam} in character system prompts.
    /// </summary>
    public string? BarName { get; set; }
    /// <summary>
    /// Set when this team begins the investigation. Players can no longer join
    /// or switch this team; administrators retain full team management access.
    /// </summary>
    public bool IsLocked { get; set; }
    /// <summary>The player who created this team; used to limit players to one team creation.</summary>
    public string? CreatedByUserId { get; set; }

    public List<TeamProgressModel> TeamProgresss { get; set; } = [];
    public List<TeamUnlockModel> TeamUnlocks { get; set; } = [];
    public List<ChatModel> Chats { get; set; } = [];
    public List<TeamWeaponModel> TeamWeapons { get; set; } = [];
    public List<TeamLogigramMarkModel> TeamLogigramMarks { get; set; } = [];
    public List<TeamSearchPictureRevealModel> TeamSearchPictureReveals { get; set; } = [];
}
