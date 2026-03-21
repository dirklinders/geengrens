namespace GeenGrens.Web.Clients;

public class ChatApiClient(HttpClient httpClient)
{
    public async Task<List<string>> GetChatsAsync(CancellationToken cancellationToken = default)
    {
        var chats = await httpClient.GetFromJsonAsAsyncEnumerable<string>("/api/chatfe/getchats?characterId=4", cancellationToken).ToListAsync(cancellationToken);
        List<string>? chatsNotnull = chats?.Where(x => x != null).Select(x => x!).ToList();

        return chatsNotnull ?? [];
    }

    public async Task<List<string>> GetNextResponse(string userInput, CancellationToken cancellationToken = default)
    {
        // Call the API and get the responses as an async enumerable
        var chats = await httpClient
            .GetFromJsonAsAsyncEnumerable<string>($"/api/chatfe/chat?characterid=4&question={Uri.EscapeDataString(userInput)}", cancellationToken)
            .ToListAsync(cancellationToken);

        // Filter out any nulls just in case
        var chatsNotNull = chats.Where(x => x != null).Select(x => x!).ToList();

        return chatsNotNull ?? new List<string>();
    }
}
