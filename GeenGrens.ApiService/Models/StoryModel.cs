namespace GeenGrens.ApiService.Models;

[GenerateCrud(true)]
public class StoryModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public List<CharacterModel> Characters { get; set; } = [];
}
