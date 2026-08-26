// Controllers/OpenAIRealtimeController.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/airealtime")]
public sealed class AIRealtimeController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly string _openAiApiKey;

    public AIRealtimeController(IHttpClientFactory httpFactory, IOptions<OpenAISettings> openAISettings)
    {
        _http = httpFactory.CreateClient();
        _openAiApiKey = openAISettings.Value.Key; 

    }

    //TODO: migrate this to the AIService interface and implementation
    [HttpPost("client-secret")]
   // tie secret issuance to an authenticated user
    public async Task<IActionResult> CreateClientSecret([FromBody] RealtimeSessionRequestDto req)
    {
        if (req == null)
            return BadRequest("Missing RealtimeSessionRequestDto.");

        var payload = new
        {
             session = new
            {
                type = "realtime",
                model = string.IsNullOrWhiteSpace(req.Model)
                    ? "gpt-4o-mini-realtime-preview-2024-12-17" //TODO: move to config
                    : req.Model,
            }
        };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post,
            "https://api.openai.com/v1/realtime/client_secrets"); //TODO: move to config
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
        httpReq.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(httpReq);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, json);

        return Content(json, "application/json");
    }
}
