using System.Text.Json;
using MedInsights.Controllers;
using MedInsights.Lib.Configurations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace TurnKeyOps.API.Controllers;

[Authorize]
[Route("api/bob")]
public sealed class BobController : ApiControllerBase
{
    private const string SystemPrompt = """
        You are Bob, the AI operating partner for a construction back office.
        Answer only from the supplied tenant-scoped operating context.
        Be concise, practical, and decisive. Lead with the answer, then give the reason.
        Rank work by safety, customer commitment, schedule risk, and cash impact.
        Never claim an action was completed unless the context says it was completed.
        Never invent customers, amounts, dates, weather, measurements, or job facts.
        For customer messages, clearly label the output as a draft.
        For estimates, quantities, schedule changes, customer commitments, payments,
        external messages, and destructive changes, recommend an approval step.
        When useful, point to Quotes, Estimates, Calendar, Jobs, Invoices, Customers,
        Website, or Settings as the place to finish the work.
        """;

    private const string AnalyzeSystemPrompt = """
        You are Bob, the AI operating layer for a construction back office.
        Return ONLY valid JSON. Do not use markdown fences.

        Identify the operator's intent as exactly one of:
        - general
        - start_estimate
        - estimate_followup

        start_estimate means creating, building, preparing, revising, or supplying facts for an internal estimate.
        estimate_followup means finding or reviewing estimates that need attention, follow-up, a response, or a next action.

        When mode is estimate-builder OR intent is start_estimate, extract every fact in the latest message
        and any clearly matching existing record in operatingContext that belongs in the estimate.
        A single message may populate multiple fields. Corrections replace prior values.
        Never use conversational statements such as "I already told you", "yes", "no", or "that's right"
        as field values unless they contain an actual business fact.
        Never invent a value. Omit fields not supported by the message or supplied context.

        Allowed estimate field keys:
        contactName, companyName, email, phone, serviceAddress, projectType,
        scope, dimensions, depth, timeline, notes.

        Return this shape:
        {
          "intent": "general|start_estimate|estimate_followup",
          "confidence": 0.0,
          "answer": "concise natural-language response",
          "fields": { "allowedField": "extracted value" },
          "suggestedReplies": ["short answer"]
        }

        suggestedReplies is optional. Include one to three short replies only when Bob's answer asks
        a constrained question with a small set of obvious answers. Omit it for open-ended questions.
        Suggested replies must be actual answers, never actions Bob already performed.

        For estimate-builder:
        - answer should acknowledge useful facts captured.
        - Do not ask a question in answer; the application selects the next required question.
        For general:
        - answer only from supplied tenant context.
        - do not claim an action was completed.
        """;

    private readonly OpenAIClient _openAIClient;
    private readonly OpenAISettings _settings;

    private static string GetVoicePrompt(string? voice) => voice?.Trim().ToLowerInvariant() switch
    {
        "friendly" => """
            VOICE: Friendly Bob. Be warm, conversational, and encouraging without becoming chatty.
            Use the operator's first name only when it is supplied naturally in context.
            """,
        "foreman" => """
            VOICE: Foreman Bob. Be blunt, decisive, and organized around priorities and next actions.
            Sound like a respected construction foreman running a clear morning huddle.
            """,
        "advisor" => """
            VOICE: Advisor Bob. Explain reasoning, tradeoffs, and downstream effects in useful detail.
            Keep the recommendation clear even when providing more context.
            """,
        "minimal" => """
            VOICE: Minimal Bob. Use the fewest words that fully answer the question.
            Prefer one recommendation and one next action. Avoid explanation unless requested.
            """,
        "gruff" => """
            VOICE: Gruff Bob. Sound like the rough, crass, teasingly condescending construction veteran
            the operator has beers with after a brutal day on site. Use frequent natural jobsite profanity
            such as damn, hell, shit, bullshit, and fuck. Be impatient with avoidable messes and bad process,
            but remain useful and loyal to the operator.

            Be actively snarky, not merely profane. When the operator uses redundant, vague, contradictory,
            or painfully obvious wording, briefly translate it into the concrete meaning and jab at them for
            making you do it. Example style: "Lakeview Place ASAP—that means this week, you dumbass." Use
            short sarcastic asides, rhetorical questions, and colorful corrections that fit the facts. Aim
            for one sharp jab in most responses; do not turn every sentence into an insult or let the joke
            obscure the action, date, amount, customer, or recommendation.

            Never use slurs, threats, sexual harassment, or identity-based insults. Never direct degrading
            abuse at a customer or coworker. Customer-facing drafts must remain professional and contain no
            profanity.
            """,
        _ => """
            VOICE: Practical Bob. Be calm, direct, concise, and focused on the next useful action.
            """
    };

    public BobController(OpenAIClient openAIClient, IOptions<OpenAISettings> settings)
    {
        _openAIClient = openAIClient;
        _settings = settings.Value;
    }

    [HttpPost("respond")]
    public async Task<IActionResult> Respond([FromBody] BobRespondRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequestResponse("Ask Bob a question.", nameof(request.Question));

        var contextJson = JsonSerializer.Serialize(request.Context ?? new Dictionary<string, object?>());
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage($"{SystemPrompt}\n\n{GetVoicePrompt(request.Voice)}"),
            new UserChatMessage(
                $"""
                CURRENT BDR OPERATING CONTEXT
                {contextJson}

                OPERATOR QUESTION
                {request.Question.Trim()}
                """)
        };

        var chatClient = _openAIClient.GetChatClient(_settings.DefaultModel);
        var completion = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        var answer = completion.Value.Content.FirstOrDefault()?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(answer))
            return BadRequestResponse("Bob did not return an answer. Try the request again.");

        return OkResponse(new BobRespondResponse(answer));
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] BobAnalyzeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequestResponse("Tell Bob what you need.", nameof(request.Message));

        var payloadJson = JsonSerializer.Serialize(new
        {
            mode = string.IsNullOrWhiteSpace(request.Mode) ? "general" : request.Mode,
            currentEstimate = request.Estimate,
            operatingContext = request.Context,
            recentConversation = request.Conversation,
            latestMessage = request.Message.Trim()
        });
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage($"{AnalyzeSystemPrompt}\n\n{GetVoicePrompt(request.Voice)}"),
            new UserChatMessage(payloadJson)
        };

        var chatClient = _openAIClient.GetChatClient(_settings.DefaultModel);
        var completion = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        var raw = completion.Value.Content.FirstOrDefault()?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return BadRequestResponse("Bob did not return an analysis. Try again.");

        try
        {
            var cleaned = raw
                .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
            var result = JsonSerializer.Deserialize<BobAnalyzeResponse>(
                cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result is null)
                return BadRequestResponse("Bob returned an empty analysis. Try again.");

            var allowedIntents = new[] { "general", "start_estimate", "estimate_followup" };
            if (!allowedIntents.Contains(result.Intent, StringComparer.OrdinalIgnoreCase))
                result.Intent = "general";

            var allowedFields = new HashSet<string>(
                new[]
                {
                    "contactName", "companyName", "email", "phone", "serviceAddress",
                    "projectType", "scope", "dimensions", "depth", "timeline", "notes"
                },
                StringComparer.OrdinalIgnoreCase);
            result.Fields = result.Fields
                .Where(item => allowedFields.Contains(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(item => item.Key, item => item.Value.Trim(), StringComparer.OrdinalIgnoreCase);
            result.Answer = result.Answer?.Trim() ?? string.Empty;
            result.SuggestedReplies = result.SuggestedReplies
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Where(item => item.Length <= 80)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();
            result.Confidence = Math.Clamp(result.Confidence, 0, 1);
            return OkResponse(result);
        }
        catch (JsonException)
        {
            return BadRequestResponse("Bob could not structure that response. Please try again.");
        }
    }
}

public sealed class BobRespondRequest
{
    public string Question { get; set; } = string.Empty;
    public string Voice { get; set; } = "practical";
    public object? Context { get; set; }
}

public sealed record BobRespondResponse(string Answer);

public sealed class BobAnalyzeRequest
{
    public string Message { get; set; } = string.Empty;
    public string Mode { get; set; } = "general";
    public string Voice { get; set; } = "practical";
    public object? Estimate { get; set; }
    public object? Context { get; set; }
    public object? Conversation { get; set; }
}

public sealed class BobAnalyzeResponse
{
    public string Intent { get; set; } = "general";
    public double Confidence { get; set; }
    public string Answer { get; set; } = string.Empty;
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SuggestedReplies { get; set; } = [];
}
