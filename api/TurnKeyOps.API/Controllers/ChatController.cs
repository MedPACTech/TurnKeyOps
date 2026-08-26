using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/chats")]
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
    public async Task<IActionResult> CreateChat([FromBody] CreateTurnKeyChatDto input, CancellationToken ct)
    {
        var result = await _service.CreateChatAsync(input, ct);
        return OkResponse(result);
    }

    [HttpPut("{chatId:guid}")]
    public async Task<IActionResult> UpdateChat(
        Guid chatId,
        [FromBody] UpdateTurnKeyChatDto input,
        CancellationToken ct) =>
        OkResponse(await _service.UpdateChatAsync(chatId, input, ct));

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

    [HttpPost("{chatId:guid}/messages/append")]
    public async Task<IActionResult> AppendMessage(
        Guid chatId,
        [FromBody] AppendTurnKeyChatMessageDto input,
        CancellationToken ct) =>
        OkResponse(await _service.AppendMessageAsync(chatId, input, ct));

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
