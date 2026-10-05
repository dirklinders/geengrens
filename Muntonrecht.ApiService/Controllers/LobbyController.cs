using System.Security.Claims;

namespace Muntonrecht.ApiService.Controllers;

/// <summary>Player-facing team lobby, available before an investigation starts.</summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LobbyController(
    UserManager<UserModel> userManager,
    MuntonrechtContext dbContext) : ControllerBase
{
    private async Task<UserModel?> CurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId == null ? null : await userManager.FindByIdAsync(userId);
    }

    [HttpGet("Teams")]
    public async Task<IActionResult> Teams()
    {
        var teams = await dbContext.Teams.OrderBy(t => t.Name).ToListAsync();
        var memberCounts = await userManager.Users
            .Where(u => u.TeamId > 0)
            .GroupBy(u => u.TeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeamId, x => x.Count);

        return Ok(teams.Select(t => new {
            id = t.Id,
            name = t.Name,
            isLocked = t.IsLocked,
            memberCount = memberCounts.GetValueOrDefault(t.Id, 0),
        }));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request)
    {
        var user = await CurrentUser();
        if (user == null) return Unauthorized();
        var isAdmin = await userManager.IsInRoleAsync(user, "admin");
        if (!isAdmin && await dbContext.Teams.AnyAsync(t => t.CreatedByUserId == user.Id))
            return Conflict("Je kunt maximaal één team aanmaken. Je kunt nog wel lid worden van een bestaand, niet gestart team.");
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Geef je team een naam.");
        if (name.Length > 60) return BadRequest("Een teamnaam mag maximaal 60 tekens bevatten.");
        if (await dbContext.Teams.AnyAsync(t => t.Name.ToLower() == name.ToLower()))
            return Conflict("Deze teamnaam bestaat al.");

        // Admin-created teams intentionally have no player creator, so admins
        // remain free to set up as many teams as the event requires.
        var team = new TeamModel { Name = name, CreatedByUserId = isAdmin ? null : user.Id };
        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync();
        user.TeamId = team.Id;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);
        return Ok(new { id = team.Id, name = team.Name, isLocked = false });
    }

    [HttpPost("Join")]
    public async Task<IActionResult> Join([FromBody] JoinTeamRequest request)
    {
        var user = await CurrentUser();
        if (user == null) return Unauthorized();
        var team = await dbContext.Teams.FindAsync(request.TeamId);
        if (team == null) return NotFound("Team niet gevonden.");
        if (team.IsLocked) return Conflict("Dit team is al gestart en is alleen nog door een beheerder aanpasbaar.");
        if (user.TeamId > 0 && user.TeamId != team.Id)
        {
            var current = await dbContext.Teams.FindAsync(user.TeamId);
            if (current?.IsLocked == true)
                return Conflict("Je huidige team is al gestart. Alleen een beheerder kan je team nog wijzigen.");
        }
        user.TeamId = team.Id;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? Ok(new { teamId = team.Id, teamName = team.Name }) : BadRequest(result.Errors);
    }

    [HttpPost("Start")]
    public async Task<IActionResult> Start()
    {
        var user = await CurrentUser();
        if (user?.TeamId is not > 0) return BadRequest("Kies of maak eerst een team.");
        var team = await dbContext.Teams.FindAsync(user.TeamId);
        if (team == null) return BadRequest("Team niet gevonden.");
        team.IsLocked = true;
        await dbContext.SaveChangesAsync();
        return Ok(new { started = true, teamName = team.Name });
    }

    public class CreateTeamRequest { public string? Name { get; set; } }
    public class JoinTeamRequest { public int TeamId { get; set; } }
}
