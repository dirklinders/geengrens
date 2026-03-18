using Microsoft.AspNetCore.Identity;

public class UserModel : IdentityUser
{
    // Add extra properties if needed
    public string FullName { get; set; }
    public int TeamId { get; set; }
}