using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
public class ChatController : ApiControllerBase
{
    private readonly ITurnKeyChatService _service;

    public ChatController(ITurnKeyChatService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetChats()
    {
        var result = await _service.GetChatsAsync();
        return OkResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateChat()
    {
        var result = await _service.CreateChatAsync();
        return OkResponse(result);
    }

    [HttpGet("{chatId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid chatId)
    {
        var result = await _service.GetMessagesAsync(chatId);
        return OkResponse(result);
    }

    [HttpPost("{chatId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid chatId, [FromBody] SendMessageRequest request)
    {
        var result = await _service.SendMessageAsync(chatId, request.Message);
        return OkResponse(result);
    }

    [HttpDelete("{chatId:guid}")]
    public async Task<IActionResult> DeleteChat(Guid chatId)
    {
        await _service.DeleteChatAsync(chatId);
        return NoContentResponse();
    }
}

public class SendMessageRequest
{
    public string Message { get; set; } = string.Empty;
}
