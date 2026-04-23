namespace GeenGrens.ApiService.Models;

[GenerateCrud(true)]
public class TeamModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Override the default notebook location for this specific team.
    /// Leave null to use the default "Onder de trap".
    /// </summary>
    public string? NotebookLocation { get; set; }
    /// <summary>
    /// When true, this team gets access to the digital playtest notebook in the game UI.
    /// </summary>
    public bool IsPlaytest { get; set; }
    /// <summary>
    /// The name of the bar used in this team's story. Replaces {BarNaam} in character system prompts.
    /// </summary>
    public string? BarName { get; set; }

    public List<TeamProgressModel> TeamProgresss { get; set; } = [];
    public List<TeamUnlockModel> TeamUnlocks { get; set; } = [];
    public List<ChatModel> Chats { get; set; } = [];
}
