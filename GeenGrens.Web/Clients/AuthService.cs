namespace GeenGrens.Web.Clients;

public class AuthService
{
    private readonly HttpClient _http;

    public AuthService(HttpClient http) => _http = http;

    public async Task<bool> IsLoggedIn()
    {
        var response = await _http.GetAsync("/api/auth/isAuthenticated"); // protected endpoint
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> LoginAsync()
    {
        return true;
    }
}
