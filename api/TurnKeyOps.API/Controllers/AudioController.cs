using Microsoft.AspNetCore.Mvc;
using MedInsights.Services;
using Microsoft.Extensions.Options;
using MedInsights.Lib.Configurations;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace MedInsights.API.Controllers
{
    // [ApiController]
    // [Authorize]
    // [Route("api/audio")]
    // public class AudioStreamController : ControllerBase
    // {
    //     private readonly IChatOrchestratorService _orchestrator;
    //     private readonly IAIService _chat;
    //     private readonly IAzureSpeechService _speech;
    //     private readonly JwtSettings _jwtSettings;
    //     private readonly IDictationService _dictationService;

    //     public AudioStreamController(IOptions<JwtSettings> jwtSettings,
    //                                  IChatOrchestratorService orchestrator,
    //                                  IOpenAIService chat, IAzureSpeechService speech, IDictationService dictationService)
    //     {
    //         _jwtSettings = jwtSettings.Value;
    //         _orchestrator = orchestrator;
    //         _chat = chat;
    //         _speech = speech;
    //         _dictationService = dictationService;
    //     }

     
    // }
}

// [ApiController]
// [Route("ws/audiochat")]
// public class AudioChatSocketController : ControllerBase
// {

//     private readonly JwtSettings _jwtSettings;
//     private readonly IChatOrchestratorService _orchestrator;
//     private readonly IOpenAIChatService _chat;
//     private readonly IOpenAIRealtimeService _openAiRealtimeService;

//     public AudioChatSocketController(IChatOrchestratorService orchestrator, IOpenAIChatService chat, IOpenAIRealtimeService openAiRealtimeService, IOptions<JwtSettings> jwtSettings)
//     {
//         _orchestrator = orchestrator;
//         _chat = chat;
//         _openAiRealtimeService = openAiRealtimeService;
//         _jwtSettings = jwtSettings.Value;
//     }

//     [HttpGet("realtime")]
//     public async Task GetRealtime()
//     {
//         if (!HttpContext.WebSockets.IsWebSocketRequest)
//         {
//             HttpContext.Response.StatusCode = 400;
//             return;
//         }

//         using var clientSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
//         using var openAiSocket = await _openAiRealtimeService.ConnectAsync();

//         // Run both pumps concurrently
//         var clientToAi = _openAiRealtimeService.PumpClientAudio(clientSocket, openAiSocket);
//         var aiToClient = _openAiRealtimeService.PumpOpenAiResponses(openAiSocket, clientSocket);

//         // Close when either direction finishes
//         await Task.WhenAny(clientToAi, aiToClient);

//         try { await clientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None); }
//         catch { }
//         try { await openAiSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None); }
//         catch { }
//     }


//     [HttpGet]
//     public async Task Get()
//     {
//         Console.WriteLine("🔌 Incoming WebSocket request...");

//         if (!HttpContext.WebSockets.IsWebSocketRequest)
//         {
//             HttpContext.Response.StatusCode = 400;
//             return;
//         }

//         var token = HttpContext.Request.Query["access_token"].ToString();
//         if (string.IsNullOrEmpty(token))
//         {
//             HttpContext.Response.StatusCode = 401;
//             return;
//         }

//         var user = ValidateToken(token);
//         if (user == null)
//         {
//             HttpContext.Response.StatusCode = 401;
//             return;
//         }
//         HttpContext.User = user;

//         // context parameters passed in query
//         var chatIdParam = HttpContext.Request.Query["chatId"].ToString();
//         Guid chatId = Guid.TryParse(chatIdParam, out var g) ? g : Guid.Empty;
//         Guid sessionId = Guid.TryParse(HttpContext.Request.Query["sessionId"].ToString(), out var s) ? s : Guid.Empty;

//         using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
//         Console.WriteLine("✅ WebSocket accepted (chatId={0}, sessionId={1})", chatId, sessionId);

//         var buffer = new byte[8192];
//         var ms = new MemoryStream();

//         while (socket.State == WebSocketState.Open)
//         {
//             var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

//             if (result.MessageType == WebSocketMessageType.Close)
//             {
//                 await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
//                 break;
//             }

//             ms.Write(buffer, 0, result.Count);

//             if (result.EndOfMessage)
//             {
//                 ms.Position = 0;
//                 await HandleAudioMessage(socket, ms, chatId, sessionId);
//                 ms.SetLength(0); // reset for next turn
//             }
//         }
//     }

//     /* ---------------- HELPERS ---------------- */

//     private async Task HandleAudioMessage(WebSocket socket, MemoryStream audioStream, Guid chatId, Guid sessionId)
//     {
//         // 1. Transcribe
//         var transcript = await _chat.TranscribeAsync(audioStream, CancellationToken.None);
//         Console.WriteLine($"📝 Transcript: {transcript}");

//         // 2. Send transcript to client
//         await SendJson(socket, new { type = "userText", text = transcript });

//         // 3. Stream assistant reply
//         var assistantReply = await StreamAssistant(socket, transcript, chatId, sessionId);

//         // 4. Synthesize & send audio
//         await SendSynthesizedAudio(socket, assistantReply);
//     }

//     private async Task<string> StreamAssistant(WebSocket socket, string transcript, Guid chatId, Guid sessionId)
//     {
//         var sb = new StringBuilder();

//         await foreach (var ev in _orchestrator.StreamChatAsync(
//                            GetTenantId(), GetUserId(),
//                            transcript,
//                            chatId, sessionId,
//                            CancellationToken.None))
//         {
//             if (ev.Token != null)
//             {
//                 sb.Append(ev.Token);
//                 await SendJson(socket, new { type = "token", text = ev.Token });
//             }
//         }

//         return sb.ToString();
//     }

//     private async Task SendSynthesizedAudio(WebSocket socket, string text)
//     {
//         using var audioOut = new MemoryStream();
//         await _chat.SynthesizeSpeechStreamAsync(text, audioOut, "alloy", "wav", CancellationToken.None);

//         var audioBytes = audioOut.ToArray();
//         Console.WriteLine($"🔊 Sending audio {audioBytes.Length} bytes");

//         await SendJson(socket, new
//         {
//             type = "audio",
//             mime = "audio/wav",
//             data = Convert.ToBase64String(audioBytes)
//         });
//     }

  
//     private async Task SendJson(WebSocket socket, object payload)
//     {
//         var json = System.Text.Json.JsonSerializer.Serialize(payload);
//         await socket.SendAsync(
//             Encoding.UTF8.GetBytes(json),
//             WebSocketMessageType.Text,
//             true,
//             CancellationToken.None
//         );
//     }


//     private ClaimsPrincipal ValidateToken(string token)
//     {
//         var tokenHandler = new JwtSecurityTokenHandler();
//         var validationParameters = new TokenValidationParameters
//         {
//             ValidateIssuerSigningKey = true,
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key)),
//             ValidateIssuer = false,
//             ValidateAudience = false,
//             ClockSkew = TimeSpan.Zero
//         };

//         try
//         {
//             var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
//             return principal;
//         }
//         catch
//         {
//             return null;
//         }
//     }

// }




