using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace GeenGrens.Web.Clients;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(HttpClient http, IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
    }


    public async Task<AuthResponse?> IsLoggedIn()
    
    {
        var cookie = _httpContextAccessor.HttpContext.Request.Cookies[".AspNetCore.Identity.Application"];

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/isAuthenticated");
        request.Headers.Add("Cookie", $".AspNetCore.Identity.Application={cookie}");

        var response = await _http.SendAsync(request);
        //var response = await _http.GetAsync("/api/auth/isAuthenticated"); // protected endpoint

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return authResponse;
    }

    public async Task<AuthenticationState> GetAuthenticationStateAsync()
    {       
        var authResponse = await IsLoggedIn();
        var identity = authResponse is null ? new ClaimsIdentity() : new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, authResponse.Name), new Claim(ClaimTypes.Email, authResponse.Email) }, "Cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}

public class AuthResponse
{
    public bool IsAuthenticated { get; set; }
    public string Email { get; set;  }
    public string Name { get; set; }    
}
