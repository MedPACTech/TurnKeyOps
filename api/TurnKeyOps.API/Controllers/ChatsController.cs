using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Text.Json;
using MedInsights.Lib.Configurations;
using MedInsights.Controllers;
using MedInsights.Services.Interfaces;
using MedInsights.Lib.Dtos;


[ApiController]
[Route("api/[controller]")]
public class ChatsController : ApiControllerBase
{

    private readonly IChatService _chatService;
    private readonly IChatOrchestratorService _orchestrator;

    private readonly HttpClient _http;
    //private readonly Guid _userId;
    private readonly string _openAiApiKey;
    private readonly IDocumentService _documentService;

    public ChatsController(IOptions<OpenAISettings> openAISettings, IChatService chatService, IChatOrchestratorService orchestrator, HttpClient http, IDocumentService documentService) : base()
    {
        _chatService = chatService;
        _orchestrator = orchestrator;
        _openAiApiKey = openAISettings.Value.Key;
        _http = http;   
        _documentService = documentService;
    }

    // GET /api/chats
    /// <summary>
    /// Gets all chats for the current user.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns>OkResponse with the list of ChatDto ordered by last updated date</returns>
    [HttpGet]
    public async Task<IActionResult> GetChatsAsync(CancellationToken ct)
    {
        var chats = await _chatService.GetChatsAsync(ct);
        return OkResponse(chats);
    }

    // GET /api/chats
    /// <summary>
    /// Gets a specific chat by ID.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    /// <returns>OkResponse with the ChatDto</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetChatByIdAsync(Guid id, CancellationToken ct)
    {
        var chat = await _chatService.GetChatByIdAsync(id, ct);
        return OkResponse(chat);
    }

    // GET /api/chats/{id}/messages
    /// <summary>
    /// Gets messages for a specific chat by ID.
    /// <param name="id">Chat ID (GUID)</param>
    /// </summary>
    /// <returns>OkResponse with the list of ChatMessageDto</returns>
    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetChatMessages(Guid id, CancellationToken ct)
    {
        var messages = await _chatService.GetChatMessagesAsync(id, 0, ct);
        if (messages is null) return NotFound();
        return OkResponse(messages);
    }

    // GET api/patients/{patientId}/chats  (provider-scoped)
    [HttpGet("api/patients/{patientId:guid}/chats")]
    public async Task<IActionResult> GetChatsByPatientIdAsync(Guid patientId, CancellationToken ct)
    {
        var chats = await _chatService.GetChatsForCurrentProviderByPatientIdAsync(patientId, ct);
        return OkResponse(chats);
    }

    // GET api/chats?patientId=... (tenant-scoped)
    [HttpGet("api/chats")]
    public async Task<IActionResult> GetChatsByPatientIdQueryAsync([FromQuery] Guid patientId, CancellationToken ct)
    {
        var chats = await _chatService.GetChatsForTenantByPatientIdAsync(patientId, ct);
        return OkResponse(chats);
    }


    // POST /api/chats
    /// <summary>
    /// Creates a new chat.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns>OkResponse with the created ChatDto</returns>
    [HttpPost]
    public async Task<IActionResult> CreateChatAsync([FromQuery] Guid? patientId, CancellationToken ct)
    {
        var chat = await _chatService.CreateChatAsync(patientId, ct);
        return OkResponse(chat);
    }


    // POST /api/chats/transcript
    /// <summary>
    /// Saves a collection of audio-to-text transcript messages to a chat
    /// (creates a new chat if needed).
    /// </summary>
    /// <param name="chatMessages">Collection of transcript messages.</param>
    /// <param name="ct"></param>
    /// <returns>OkResponse with the saved ChatMessageDto collection</returns>
    [HttpPost("transcript")]
    public async Task<IActionResult> SaveTranscriptAsync(
        [FromQuery] Guid chatId,
        [FromBody] IEnumerable<ChatMessageDto> chatMessages,
        CancellationToken ct)
    {
        if (chatMessages is null || !chatMessages.Any())
            return BadRequest("At least one transcript message is required.");

        // Optional: validate each message has content
        if (chatMessages.Any(m => m is null || string.IsNullOrWhiteSpace(m.Content)))
            return BadRequest("All transcript messages must have content.");

        var result = await _orchestrator.SaveTranscriptAsync(chatId, chatMessages.ToList(), ct);

        // Assuming orchestrator returns the saved messages
        return OkResponse(result);
    }

    // POST /api/chats/{documentId}
    /// <summary>
    /// Updates attached documents for a specific chat.
    /// </summary>
    /// <param name="chatId"></param>
    /// <param name="attachedDocuments"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("{chatId:guid}/documents")]
    public async Task<IActionResult> UpdateAttachedDocuments(
        Guid chatId,
        [FromBody] List<Guid> attachedDocuments,
        CancellationToken ct)
    {
        if (chatId == Guid.Empty)
            return BadRequest("ChatId is required.");

        if (attachedDocuments == null)
            return BadRequest("AttachedDocuments is required.");

        var result = await _chatService.UpdateAttachedDocumentsAsync(
            chatId,
            attachedDocuments,
            ct);

        return OkResponse(result);
    }

    // DELETE /api/chats/{id}
    /// <summary>
    /// Deletes a specific chat by ID.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    /// <returns>NoContent on success, NotFound if chat doesn't exist</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteChat(Guid id, CancellationToken ct)
    {
        var result = await _chatService.DeleteChatAsync(id, ct);
        if (!result) return NotFound();

        return DeletedResponse(result);
    }

    //TODO: certain tools may require cascading tools to be run, especially with resuming conversations.
    //TODO: check how we handle errors from orchestrator and streaming, handle in helper
    /// <summary>
    /// Streams chat completions safely through the backend, preserving full tool and message structures.
    /// 
    /// </summary>
    /// <param name="payload">The full payload to send to OpenAI, including 'messages' and optional parameters like 'model', 'temperature', etc.</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Streams the response directly to the client as Server-Sent Events (SSE)</returns>
    [HttpPost("completion")]
    public async Task StreamChatCompletion([FromBody] JsonElement payload, CancellationToken ct)
    {

        try
        {
            await _orchestrator.StreamChatResponseAsync(payload, Response, ct);
        }
        catch (Exception ex)
        {
            //Response.StatusCode = 500;
            await Response.WriteAsync($"{{\"error\": \"{ex.Message}\"}}");
            return;
        }
    }

    /// <summary>
    /// Streams non-persistent chat completions.
    /// Requires payload.sessionId to maintain short-lived per-session context.
    /// </summary>
    [HttpPost("completion/transient")]
    [Authorize]
    public async Task StreamTransientChatCompletion([FromBody] JsonElement payload, CancellationToken ct)
    {
        try
        {
            await _orchestrator.StreamTransientChatResponseAsync(payload, Response, ct);
        }
        catch (Exception ex)
        {
            await Response.WriteAsync($"{{\"error\": \"{ex.Message}\"}}");
            return;
        }
    }

    //TODO: StreamAudioChatCompletion recieved audio messages over websocket, transcribe, send to orchestrator, get response, and send back text response, send back over websocket


}


