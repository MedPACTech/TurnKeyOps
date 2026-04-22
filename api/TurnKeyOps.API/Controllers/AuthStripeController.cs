using System.Net.Http.Json;
using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Stripe;

namespace MedInsights.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthStripeController : ApiControllerBase
{
    private readonly StripeClient _stripe;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SystemSettings _systemSettings;

    public AuthStripeController(
        StripeClient stripe,
        IHttpClientFactory httpClientFactory,
        IOptions<SystemSettings> systemSettings)
    {
        _stripe = stripe;
        _httpClientFactory = httpClientFactory;
        _systemSettings = systemSettings.Value;
    }

    [HttpPost("registerwithstripe")]
    [AllowAnonymous]
    public async Task<RegisterResponse> RegisterWithStripe([FromBody] RegisterWithStripeRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.SessionId))
            throw new BadHttpRequestException("SessionId is required.");

        var sessionService = new Stripe.Checkout.SessionService(_stripe);
        var session = await sessionService.GetAsync(dto.SessionId, cancellationToken: ct);

        if (!string.Equals(session.Status, "complete", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stripe checkout session is not complete.");

        var email = FirstNonEmpty(
            dto.EmailOverride,
            session.CustomerDetails?.Email,
            session.CustomerEmail);

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Stripe checkout did not provide a customer email.");

        var displayName = FirstNonEmpty(
            dto.DisplayNameOverride,
            session.CustomerDetails?.Name);

        var resetUrlBase = $"{_systemSettings.APIHost.TrimEnd('/')}/api/auth/email-validation";

        var client = _httpClientFactory.CreateClient("MedInsightsApi");
        using var response = await client.PostAsJsonAsync(
            "/api/auth/start-email-password-registration",
            new
            {
                Email = email,
                DisplayName = displayName,
                ResetUrlBase = resetUrlBase
            },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw CreateRegistrationStartException(response, detail);
        }

        return new RegisterResponse(
            UserId: Guid.Empty,
            Email: email,
            EmailOnFile: true,
            EmailConfirmed: false,
            PhoneNumber: null,
            PhoneOnFile: false,
            PhoneConfirmed: false,
            AvailableLoginChannels: ["email"]);
    }

    [HttpGet("email-validation")]
    [AllowAnonymous]
    public IActionResult EmailValidationRedirect(
        [FromQuery] string? challengeId,
        [FromQuery] string? token,
        [FromQuery] string? email,
        [FromQuery] string? name)
    {
        var destination = BuildFrontendEmailValidationUrl(challengeId, token, email, name);
        return Redirect(destination);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private string BuildFrontendEmailValidationUrl(
        string? challengeId,
        string? token,
        string? email,
        string? name)
    {
        var frontEndBase = $"{_systemSettings.ApplicationHost.TrimEnd('/')}/emailValidation";
        var query = BuildConfirmationQueryString(challengeId, token, email, name);
        return $"{frontEndBase}?{query}#confirmation?{query}";
    }

    private static string BuildConfirmationQueryString(
        string? challengeId,
        string? token,
        string? email,
        string? name)
    {
        var values = new List<KeyValuePair<string, string?>>();

        Append(values, "challengeId", challengeId);
        Append(values, "challenge", challengeId);
        Append(values, "id", challengeId);
        Append(values, "token", token);
        Append(values, "code", token);
        Append(values, "email", email);
        Append(values, "name", name);
        Append(values, "displayName", name);

        return string.Join(
            "&",
            values.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
    }

    private static void Append(ICollection<KeyValuePair<string, string?>> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(new KeyValuePair<string, string?>(key, value));
        }
    }

    private static Exception CreateRegistrationStartException(HttpResponseMessage response, string? detail)
    {
        var message = ExtractApiErrorMessage(detail);
        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"Unable to start email registration ({(int)response.StatusCode} {response.ReasonPhrase}).";
        }

        if (ContainsEmailDomainLinkError(message) || ContainsEmailDomainLinkError(detail))
        {
            return new ValidationException(
            [
                new ApiError
                {
                    Code = "RegistrationEmailUnavailable",
                    Field = "email",
                    Message = "We couldn't send the registration email because the configured sender domain is not linked in Azure Communication Services."
                }
            ]);
        }

        return new InvalidOperationException(message);
    }

    private static string? ExtractApiErrorMessage(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return null;

        try
        {
            using var document = JsonDocument.Parse(detail);
            if (!document.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
                return detail;

            foreach (var error in errors.EnumerateArray())
            {
                if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString();
            }
        }
        catch (JsonException)
        {
            return detail;
        }

        return detail;
    }

    private static bool ContainsEmailDomainLinkError(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && (value.Contains("DomainNotLinked", StringComparison.OrdinalIgnoreCase)
               || value.Contains("sender domain has not been linked", StringComparison.OrdinalIgnoreCase)
               || value.Contains("Email provider endpoint/resource not found", StringComparison.OrdinalIgnoreCase));
}
