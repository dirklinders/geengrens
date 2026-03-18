using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GeenGrens.ApiService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ChatFEController(ChatFEManager _chatManager) : ControllerBase
{
    [HttpGet("chat")]
    public async Task<IActionResult> GetChat(int characterId,string question)
    {
        var response = await _chatManager.GetChatResponseAsync(characterId,question, "default");
        return Ok(response);
    }

    [HttpGet("getchats")]
    public IActionResult GetChats(int characterId)
    {
        var response = _chatManager.GetCurrentChats(characterId);
        return Ok(response);
    }
}
