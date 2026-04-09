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

    public List<TeamProgressModel> TeamProgresss { get; set; } = [];
    public List<TeamUnlockModel> TeamUnlocks { get; set; } = [];
}
