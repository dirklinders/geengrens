using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace GeenGrens.ApiService.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class ChatFEController(
    ChatFEManager _chatManager,
    UserManager<UserModel> _userManager) : ControllerBase
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<int?> GetTeamIdAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return null;
        var user = await _userManager.FindByIdAsync(userId);
        return user?.TeamId > 0 ? user.TeamId : null;
    }

    private bool IsAdmin() =>
        User.IsInRole("admin") || User.HasClaim("role", "admin");

    // ── Get chat history (team-scoped) ───────────────────────────────────────

    [HttpGet("getchats")]
    public async Task<IActionResult> GetChats(int characterId)
    {
        var teamId = await GetTeamIdAsync();
        var chats = _chatManager.GetCurrentChats(characterId, teamId);
        return Ok(chats);
    }

    // ── SSE streaming – team chat ─────────────────────────────────────────────

    [HttpGet("stream")]
    public async Task StreamChat(int characterId, string question, CancellationToken cancellationToken)
    {
        var teamId = await GetTeamIdAsync();

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var chunk in _chatManager.StreamAsync(characterId, question, teamId, cancellationToken))
            {
                var payload = JsonSerializer.Serialize(new { text = chunk });
                await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — stream ends, DB save is still attempted in manager
        }
        catch (Exception ex)
        {
            var err = JsonSerializer.Serialize(new { error = ex.Message });
            await Response.WriteAsync($"data: {err}\n\n", CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }
    }

    // ── SSE streaming – admin test chat (no DB, admin only) ──────────────────

    [HttpPost("admin-test-stream")]
    public async Task AdminTestStream([FromBody] AdminTestStreamRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            Response.StatusCode = 403;
            return;
        }

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var chunk in _chatManager.StreamAdminTestAsync(
                request.CharacterId, request.Question, request.History, cancellationToken))
            {
                var payload = JsonSerializer.Serialize(new { text = chunk });
                await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            var err = JsonSerializer.Serialize(new { error = ex.Message });
            await Response.WriteAsync($"data: {err}\n\n", CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public class AdminTestStreamRequest
    {
        public int CharacterId { get; set; }
        public string Question { get; set; } = string.Empty;
        public List<AdminMessageDTO> History { get; set; } = [];
    }
}
