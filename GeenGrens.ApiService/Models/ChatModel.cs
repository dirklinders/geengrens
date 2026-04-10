namespace GeenGrens.ApiService.Models;

[GenerateCrud(true)]
public class ChatModel
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int CharacterId { get; set; }
    public CharacterModel Character { get; set; } = null!;
    public int? TeamId { get; set; }
    public TeamModel? Team { get; set; }
}
