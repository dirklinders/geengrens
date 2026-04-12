using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace GeenGrens.ApiService.Controllers;

[Route("api/[controller]")]
public class ExternalAuthController(SignInManager<UserModel> _signInManager, UserManager<UserModel> _userManager) : ControllerBase
{
    private string FEUrl = "https://localhost:3000";
    [HttpGet("LoginGoogle")]
    public async Task<IActionResult> LoginGoogle(string returnUrl = "/")
    {
        var location = Request.Host;
        Console.WriteLine($"Request from: {location}");
        var redirectUrl = Url.Action("ExternalLoginCallback", "ExternalAuth", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
        return Challenge(properties, "Google");
    }

    [HttpGet("ExternalLoginCallback")]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "")
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
#if DEBUG
            return Redirect($"{FEUrl}/login"); // fallback
#else
            return Redirect("/login"); // fallback
#endif
        }

        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
        if (result.Succeeded)
        {
#if DEBUG
            return Redirect($"{FEUrl}/{returnUrl}"); // redirect to FE
#else
            return LocalRedirect(returnUrl);
#endif
        }

        // First-time login → create user
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (email != null)
        {
            var name = info.Principal.FindFirstValue(ClaimTypes.Name);
            var user = new UserModel { UserName = email, Email = email, FullName = name ?? string.Empty};
            

            var createResult = await _userManager.CreateAsync(user);
            if (createResult.Succeeded)
            {
                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, isPersistent: false);
#if DEBUG
                return Redirect($"{FEUrl}/{returnUrl}"); // redirect to FE
#else
            return LocalRedirect(returnUrl);
#endif
            }
        }

#if DEBUG
        return Redirect($"{FEUrl}/register"); // fallback
#else
            return Redirect("/register"); // fallback
#endif
    }
}
