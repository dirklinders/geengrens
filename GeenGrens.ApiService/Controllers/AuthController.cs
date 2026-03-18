using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace GeenGrens.ApiService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(UserManager<UserModel> _userManager,  SignInManager<UserModel> _signInManager) : ControllerBase
{
    [HttpPost("Login")]
    public async Task<IActionResult> Login(LoginDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // 1. Find user
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return Unauthorized("Invalid email or password");

        // 2. Attempt sign in
        var result = await _signInManager.PasswordSignInAsync(
            user,
            dto.Password,
            dto.RememberMe,
            lockoutOnFailure: true
        );

        // 3. Handle result
        if (result.Succeeded)
        {
            return Ok(new
            {
                message = "Login successful"
            });
        }

        if (result.IsLockedOut)
        {
            return Unauthorized("Account locked. Try again later.");
        }

        if (result.IsNotAllowed)
        {
            return Unauthorized("Login not allowed.");
        }

        return Unauthorized("Invalid email or password");
    }
    [Authorize]
    [HttpGet("IsAuthenticated")]
    public async Task<IActionResult> IsAuthenticated() {         
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            return Ok(new
            {
                isAuthenticated = true,
                email = user.Email
            });
        }
        return Ok(new { isAuthenticated = false });
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new
        {
            message = "Logout successful"
        });
    }

}

public class LoginDTO
{
    public string Email { get; set; }
    public string Password { get; set; }
    public bool RememberMe { get; set; }
}
