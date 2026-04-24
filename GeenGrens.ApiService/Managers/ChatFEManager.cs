
using OpenAI.Chat;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace GeenGrens.ApiService.Managers;

public record ChatDTOREcord(string Role, string Message);
public record AdminMessageDTO(string Role, string Content);

/// <summary>
/// A single chunk yielded by StreamAsync / StreamAdminTestAsync.
/// Either carries a text token OR signals that the AI ended the conversation.
/// </summary>
public record ChatStreamChunk(string? Text, bool Ended = false);

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

    // ── Stop-condition tool ───────────────────────────────────────────────────

    /// <summary>
    /// Marker written to DB (Role = "System") when the AI ends the conversation.
    /// The frontend checks for this on load to restore the ended state.
    /// </summary>
    public const string EndedMarker = "[BEËINDIGD]";

    /// <summary>
    /// The OpenAI function tool that the character can call to end the conversation.
    /// Define its trigger conditions in the system prompt under "system call".
    /// </summary>
    private static readonly ChatTool _endConversationTool = ChatTool.CreateFunctionTool(
        functionName: "beeindig_gesprek",
        functionDescription: "Beëindigt het gesprek op een natuurlijk eindpunt. Roep aan als sluitende actie nadat je je afsluitende tekst hebt geschreven.",
        functionParameters: BinaryData.FromString("""{"type":"object","properties":{},"required":[]}""")
    );

    private static readonly ChatCompletionOptions _toolOptions = new()
    {
        Tools = { _endConversationTool }
    };

    // ── Streaming – team chat (saves to DB after stream completes) ───────────

    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        int characterId,
        string userInput,
        int? teamId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var character = await _geenGrensContext.Characters.FindAsync([characterId], cancellationToken);
        if (character == null)
            throw new Exception("Character not found");

        // Resolve bar name for {BarNaam} placeholder substitution
        string barName = "de bar";
        if (teamId.HasValue)
        {
            var team = await _geenGrensContext.Teams.FindAsync([teamId.Value], cancellationToken);
            if (!string.IsNullOrWhiteSpace(team?.BarName))
                barName = team.BarName;
        }
        var systemPrompt = (character.SystemPrompt ?? "").Replace("{BarNaam}", barName);

        // Build message history from DB (skip system-role markers)
        var previousChats = _geenGrensContext.Chats
            .Where(x => x.CharacterId == characterId && x.TeamId == teamId && x.Role != "System")
            .OrderBy(x => x.Id)
            .ToList();

        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };

        foreach (var chat in previousChats)
        {
            messages.Add(chat.Role == "User"
                ? new UserChatMessage(chat.Message)
                : (ChatMessage)new AssistantChatMessage(chat.Message));
        }
        messages.Add(new UserChatMessage(userInput));

        // Stream from OpenAI with the stop-condition tool available
        var fullResponse = new StringBuilder();
        bool conversationEnded = false;

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, _toolOptions, cancellationToken))
        {
            // Stream text tokens
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    fullResponse.Append(part.Text);
                    yield return new ChatStreamChunk(part.Text);
                }
            }

            // Detect tool call finish
            if (update.FinishReason == ChatFinishReason.ToolCalls)
                conversationEnded = true;
        }

        // Persist to DB
        var assistantReply = fullResponse.ToString();
        if (!string.IsNullOrEmpty(assistantReply) || conversationEnded)
        {
            _geenGrensContext.Chats.Add(new ChatModel { CharacterId = characterId, TeamId = teamId, Role = "User",      Message = userInput });

            if (!string.IsNullOrEmpty(assistantReply))
                _geenGrensContext.Chats.Add(new ChatModel { CharacterId = characterId, TeamId = teamId, Role = "Assistant", Message = assistantReply });

            if (conversationEnded)
                _geenGrensContext.Chats.Add(new ChatModel { CharacterId = characterId, TeamId = teamId, Role = "System",    Message = EndedMarker });

            await _geenGrensContext.SaveChangesAsync(CancellationToken.None);
        }

        // Signal end to frontend after all text has been flushed
        if (conversationEnded)
            yield return new ChatStreamChunk(null, Ended: true);
    }

    // ── Streaming – admin test chat (NO DB reads/writes, pure in-memory) ─────

    public async IAsyncEnumerable<ChatStreamChunk> StreamAdminTestAsync(
        int characterId,
        string userInput,
        IEnumerable<AdminMessageDTO> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var character = await _geenGrensContext.Characters.FindAsync([characterId], cancellationToken);
        if (character == null)
            throw new Exception("Character not found");

        var messages = new List<ChatMessage> { new SystemChatMessage(character.SystemPrompt) };

        foreach (var msg in history)
        {
            messages.Add(msg.Role == "User"
                ? new UserChatMessage(msg.Content)
                : (ChatMessage)new AssistantChatMessage(msg.Content));
        }
        messages.Add(new UserChatMessage(userInput));

        bool conversationEnded = false;

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, _toolOptions, cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return new ChatStreamChunk(part.Text);
            }

            if (update.FinishReason == ChatFinishReason.ToolCalls)
                conversationEnded = true;
        }

        if (conversationEnded)
            yield return new ChatStreamChunk(null, Ended: true);

        // No DB writes
    }
}
