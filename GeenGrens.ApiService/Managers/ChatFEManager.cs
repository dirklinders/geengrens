
using OpenAI.Chat;
using System.Runtime.CompilerServices;

namespace GeenGrens.ApiService.Managers;

public record ChatDTOREcord(string Role, string Message);
public record AdminMessageDTO(string Role, string Content);

public class ChatFEManager
{
    private readonly ChatClient _chatClient;
    private readonly GeenGrensContext _geenGrensContext;

    public ChatFEManager(ChatClient chatClient, GeenGrensContext geenGrensContext)
    {
        _chatClient = chatClient;
        _geenGrensContext = geenGrensContext;
    }

    // ── Team-scoped history ──────────────────────────────────────────────────

    public List<ChatDTOREcord> GetCurrentChats(int characterId, int? teamId)
    {
        var chats = _geenGrensContext.Chats
            .Where(x => x.CharacterId == characterId && x.TeamId == teamId)
            .OrderBy(x => x.Id)
            .ToList();
        return chats.Select(x => new ChatDTOREcord(x.Role, x.Message)).ToList();
    }

    // ── Streaming – team chat (saves to DB after stream completes) ───────────

    public async IAsyncEnumerable<string> StreamAsync(
        int characterId,
        string userInput,
        int? teamId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var character = await _geenGrensContext.Characters.FindAsync([characterId], cancellationToken);
        if (character == null)
            throw new Exception("Character not found");

        // Build message history from DB
        var previousChats = _geenGrensContext.Chats
            .Where(x => x.CharacterId == characterId && x.TeamId == teamId)
            .OrderBy(x => x.Id)
            .ToList();

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(character.SystemPrompt)
        };

        foreach (var chat in previousChats)
        {
            messages.Add(chat.Role == "User"
                ? new UserChatMessage(chat.Message)
                : (ChatMessage)new AssistantChatMessage(chat.Message));
        }
        messages.Add(new UserChatMessage(userInput));

        // Stream from OpenAI
        var fullResponse = new System.Text.StringBuilder();

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    fullResponse.Append(part.Text);
                    yield return part.Text;
                }
            }
        }

        // Save both messages to DB after stream completes
        var assistantReply = fullResponse.ToString();
        if (!string.IsNullOrEmpty(assistantReply))
        {
            _geenGrensContext.Chats.Add(new ChatModel
            {
                CharacterId = characterId,
                TeamId = teamId,
                Role = "User",
                Message = userInput
            });
            _geenGrensContext.Chats.Add(new ChatModel
            {
                CharacterId = characterId,
                TeamId = teamId,
                Role = "Assistant",
                Message = assistantReply
            });
            await _geenGrensContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    // ── Streaming – admin test chat (NO DB reads/writes, pure in-memory) ─────

    public async IAsyncEnumerable<string> StreamAdminTestAsync(
        int characterId,
        string userInput,
        IEnumerable<AdminMessageDTO> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var character = await _geenGrensContext.Characters.FindAsync([characterId], cancellationToken);
        if (character == null)
            throw new Exception("Character not found");

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(character.SystemPrompt)
        };

        foreach (var msg in history)
        {
            messages.Add(msg.Role == "User"
                ? new UserChatMessage(msg.Content)
                : (ChatMessage)new AssistantChatMessage(msg.Content));
        }
        messages.Add(new UserChatMessage(userInput));

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return part.Text;
            }
        }
        // No DB writes
    }
}
