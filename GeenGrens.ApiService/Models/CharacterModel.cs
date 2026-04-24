using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeenGrens.ApiService.Models;

[GenerateCrud(true)]
public class CharacterModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string? Personality { get; set; }

    /// <summary>Keyword(s) to detect in assistant replies that confirm the alibi has been mentioned.</summary>
    public string? StopKeywordAlibi { get; set; }

    /// <summary>Keyword(s) to detect in assistant replies that confirm the connection has been mentioned.</summary>
    public string? StopKeywordConnection { get; set; }

    /// <summary>Keyword(s) to detect in assistant replies that confirm the hint has been mentioned.</summary>
    public string? StopKeywordHint { get; set; }

    public List<ChatModel> Chats { get; set; } = [];
    public List<LocationCodeModel> LocationCodes { get; set; } = [];

}
