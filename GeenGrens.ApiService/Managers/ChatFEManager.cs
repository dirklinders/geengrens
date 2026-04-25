
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
    /// Marker written to DB (Role = "System") by the admin to request a graceful wrap-up.
    /// On the next player message the character closes the conversation and EndedMarker is written.
    /// </summary>
    public const string SluitAfMarker = "[SLUIT_AF]";

    /// <summary>
    /// Admin-triggered: inserts the SluitAfMarker for a team+character chat.
    /// No-ops if the conversation is already ended or a close was already requested.
    /// Returns true when the marker was inserted.
    /// </summary>
    public async Task<bool> RequestEndAsync(int characterId, int teamId)
    {
        bool alreadyEnded = _geenGrensContext.Chats
            .Any(x => x.CharacterId == characterId && x.TeamId == teamId
                      && x.Role == "System" && x.Message == EndedMarker);
        if (alreadyEnded) return false;

        bool alreadyRequested = _geenGrensContext.Chats
            .Any(x => x.CharacterId == characterId && x.TeamId == teamId
                      && x.Role == "System" && x.Message == SluitAfMarker);
        if (alreadyRequested) return false;

        _geenGrensContext.Chats.Add(new ChatModel
        {
            CharacterId = characterId,
            TeamId = teamId,
            Role = "System",
            Message = SluitAfMarker
        });
        await _geenGrensContext.SaveChangesAsync();
        return true;
    }

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

        // ── Check for admin-requested graceful close ──────────────────────────────
        bool closingRequested = teamId.HasValue && _geenGrensContext.Chats
            .Any(x => x.CharacterId == characterId && x.TeamId == teamId
                      && x.Role == "System" && x.Message == SluitAfMarker);

        if (closingRequested)
        {
            systemPrompt += "\n\n[AFSLUITINGSINSTRUCTIE: De spelleider heeft besloten dit gesprek te beëindigen. Dit is jouw laatste reactie. Reageer kort op het laatste bericht van de speler, neem dan vriendelijk maar definitief afscheid in jouw eigen stijl. Stel geen nieuwe vragen. Sluit af.]";
        }

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

        // ── Phase 1: stream text response (no tools → model always produces text) ──
        var fullResponse = new StringBuilder();

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    fullResponse.Append(part.Text);
                    yield return new ChatStreamChunk(part.Text);
                }
            }
        }

        var assistantReply = fullResponse.ToString();

        // Persist user message + assistant reply to DB
        if (!string.IsNullOrEmpty(assistantReply))
        {
            _geenGrensContext.Chats.Add(new ChatModel { CharacterId = characterId, TeamId = teamId, Role = "User",      Message = userInput });
            _geenGrensContext.Chats.Add(new ChatModel { CharacterId = characterId, TeamId = teamId, Role = "Assistant", Message = assistantReply });
            await _geenGrensContext.SaveChangesAsync(CancellationToken.None);
        }

        // ── Admin-requested close: lock the conversation and skip Phase 2 ─────────
        if (closingRequested && !string.IsNullOrEmpty(assistantReply))
        {
            _geenGrensContext.Chats.Add(new ChatModel { CharacterId = characterId, TeamId = teamId, Role = "System", Message = EndedMarker });
            await _geenGrensContext.SaveChangesAsync(CancellationToken.None);
            yield return new ChatStreamChunk(null, Ended: true);
            yield break;
        }

        // ── Phase 2: stop-condition check ────────────────────────────────────────
        // Keywords are a cheap gate: only do the model call when all three are present.
        // The model then decides whether this is actually a natural end point.
        int userMessageCount = previousChats.Count(c => c.Role == "User") + 1;

        if (userMessageCount >= 5
            && !string.IsNullOrEmpty(assistantReply)
            && !string.IsNullOrWhiteSpace(character.StopKeywordAlibi)
            && !string.IsNullOrWhiteSpace(character.StopKeywordConnection)
            && !string.IsNullOrWhiteSpace(character.StopKeywordHint))
        {
            var combined = string.Join(" ",
                previousChats.Where(c => c.Role == "Assistant").Select(c => c.Message)
                             .Append(assistantReply)
            ).ToLowerInvariant();

            bool keywordsAllMet =
                combined.Contains(character.StopKeywordAlibi.ToLowerInvariant()) &&
                combined.Contains(character.StopKeywordConnection.ToLowerInvariant()) &&
                combined.Contains(character.StopKeywordHint.ToLowerInvariant());

            // Skip the check if Danny's last response ends with a question —
            // an open question is never a natural conversation end point.
            bool endsWithQuestion = assistantReply.TrimEnd().EndsWith('?');

            if (keywordsAllMet && !endsWithQuestion)
            {
                // Keywords are met — ask the model if this is a natural end point.
                // Append the assistant reply + hidden [check] trigger; model calls the
                // tool only when the conversation genuinely feels finished.
                messages.Add(new AssistantChatMessage(assistantReply));
                messages.Add(new UserChatMessage("[check]"));

                var checkResult = await _chatClient.CompleteChatAsync(messages, _toolOptions, cancellationToken);
                bool conversationEnded = checkResult.Value.FinishReason == ChatFinishReason.ToolCalls;

                if (conversationEnded)
                {
                    _geenGrensContext.Chats.Add(new ChatModel { CharacterId = characterId, TeamId = teamId, Role = "System", Message = EndedMarker });
                    await _geenGrensContext.SaveChangesAsync(CancellationToken.None);
                    yield return new ChatStreamChunk(null, Ended: true);
                }
            }
        }
    }

    // ── Streaming – admin test chat (NO DB reads/writes, pure in-memory) ─────

    public async IAsyncEnumerable<ChatStreamChunk> StreamAdminTestAsync(
        int characterId,
        string userInput,
        IEnumerable<AdminMessageDTO> history,
        bool closingRequested = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var character = await _geenGrensContext.Characters.FindAsync([characterId], cancellationToken);
        if (character == null)
            throw new Exception("Character not found");

        // Materialise once so we can iterate multiple times
        var historyList = history.ToList();

        var testSystemPrompt = character.SystemPrompt ?? string.Empty;
        if (closingRequested)
        {
            testSystemPrompt += "\n\n[AFSLUITINGSINSTRUCTIE: De spelleider heeft besloten dit gesprek te beëindigen. Dit is jouw laatste reactie. Reageer kort op het laatste bericht van de speler, neem dan vriendelijk maar definitief afscheid in jouw eigen stijl. Stel geen nieuwe vragen. Sluit af.]";
        }

        var messages = new List<ChatMessage> { new SystemChatMessage(testSystemPrompt) };

        foreach (var msg in historyList)
        {
            messages.Add(msg.Role == "User"
                ? new UserChatMessage(msg.Content)
                : (ChatMessage)new AssistantChatMessage(msg.Content));
        }
        messages.Add(new UserChatMessage(userInput));

        // Phase 1: stream text (no tools)
        var fullResponse = new StringBuilder();

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    fullResponse.Append(part.Text);
                    yield return new ChatStreamChunk(part.Text);
                }
            }
        }

        // If admin requested close in test mode, skip Phase 2 and signal ended
        if (closingRequested)
        {
            yield return new ChatStreamChunk(null, Ended: true);
            yield break;
        }

        // Phase 2: keywords as gate, model as quality check (same pattern as game path)
        if (!string.IsNullOrWhiteSpace(character.StopKeywordAlibi)
            && !string.IsNullOrWhiteSpace(character.StopKeywordConnection)
            && !string.IsNullOrWhiteSpace(character.StopKeywordHint))
        {
            var combined = string.Join(" ",
                historyList.Where(m => m.Role == "Assistant").Select(m => m.Content)
                           .Append(fullResponse.ToString())
            ).ToLowerInvariant();

            bool keywordsAllMet =
                combined.Contains(character.StopKeywordAlibi.ToLowerInvariant()) &&
                combined.Contains(character.StopKeywordConnection.ToLowerInvariant()) &&
                combined.Contains(character.StopKeywordHint.ToLowerInvariant());

            bool endsWithQuestion = fullResponse.ToString().TrimEnd().EndsWith('?');

            if (keywordsAllMet && !endsWithQuestion)
            {
                messages.Add(new AssistantChatMessage(fullResponse.ToString()));
                messages.Add(new UserChatMessage("[check]"));

                var checkResult = await _chatClient.CompleteChatAsync(messages, _toolOptions, cancellationToken);
                if (checkResult.Value.FinishReason == ChatFinishReason.ToolCalls)
                    yield return new ChatStreamChunk(null, Ended: true);
            }
        }

        // No DB writes
    }
}
