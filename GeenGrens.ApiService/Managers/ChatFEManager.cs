
using OpenAI.Conversations;
using System.ClientModel;

namespace GeenGrens.ApiService.Managers;

public class ChatFEManager
{
    private readonly ChatClient _chatClient;
    private List<ChatMessage> _history;
    private GeenGrensContext _geenGrensContext;

    public ChatFEManager(ChatClient chatClient, GeenGrensContext geenGrensContext)
    {
        _chatClient = chatClient;
        
        _geenGrensContext = geenGrensContext;
    }
    public async Task<List<string>> GetChatResponseAsync(int characterId, string userInput, string characterName)
    {
        var character = _geenGrensContext.Characters.FirstOrDefault(x => x.Id == characterId);
        if (character == null)
        {
            throw new Exception("Character not found");
        }
        
        var newUserChat = new ChatModel
        {
            CharacterId = characterId,
            Message = userInput,
            Role = "User"
        };

        

        var previousChats = _geenGrensContext.Chats.Where(x => x.CharacterId == characterId).ToList().Select<ChatModel,ChatMessage>(x => x.Role == "User" ? new UserChatMessage(x.Message) : new AssistantChatMessage(x.Message));
        previousChats = previousChats.Prepend(new SystemChatMessage(character.SystemPrompt)).Append(new UserChatMessage(userInput));


        ChatCompletion completion = _chatClient.CompleteChat(previousChats);
        var completionText = completion.Content[0].Text;
        
        var newAssistantChat = new ChatModel
        {
            CharacterId = characterId,
            Message = completionText,
            Role = "Assistant"
        };
        _geenGrensContext.Chats.Add(newUserChat);
        _geenGrensContext.Chats.Add(newAssistantChat);

        await _geenGrensContext.SaveChangesAsync();

        return GetCurrentChats(characterId);
       
    }

    public List<string> GetCurrentChats(int characterId)
    {
        var chats = _geenGrensContext.Chats.Where(x => x.CharacterId == characterId).ToList();
        return chats.Select(x => x.Message).ToList();
    }



}
