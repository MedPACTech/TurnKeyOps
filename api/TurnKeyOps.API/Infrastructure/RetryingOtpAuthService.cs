using System.Globalization;
using System.Reflection;
using Azure;
using Azure.Data.Tables;
using IBeam.Identity.Interfaces;
using IBeam.Identity.Models;
using IBeam.Identity.Services.Otp;

namespace MedInsights.API.Infrastructure;

public sealed class RetryingOtpAuthService : IIdentityOtpAuthService
{
    private const string LocalBypassCode = "123456";

    private readonly IIdentityOtpAuthService _inner;
    private readonly ILogger<RetryingOtpAuthService> _logger;
    private readonly bool _localOtpBypassEnabled;
    private readonly string _otpHashSalt;
    private readonly TableClient? _otpChallengeTable;

    public RetryingOtpAuthService(
        IIdentityOtpAuthService inner,
        ILogger<RetryingOtpAuthService> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _inner = inner;
        _logger = logger;

        _localOtpBypassEnabled = environment.IsDevelopment() || environment.IsEnvironment("Local");
        _otpHashSalt = configuration["IBeam:Identity:Otp:HashSalt"] ?? string.Empty;

        var connectionString =
            configuration["IBeam:Identity:AzureTable:StorageConnectionString"]
            ?? configuration["IBeam:Repositories:AzureTables:ConnectionString"];
        var tablePrefix = configuration["IBeam:Identity:AzureTable:TablePrefix"] ?? "Auth";

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var serviceClient = new TableServiceClient(connectionString);
            _otpChallengeTable = serviceClient.GetTableClient($"{tablePrefix}OtpChallenges");
        }
    }

    public async Task<OtpChallengeResult> StartOtpAsync(string destination, Guid? tenantId, CancellationToken ct)
    {
        try
        {
            return await _inner.StartOtpAsync(destination, tenantId, ct);
        }
        catch (Exception ex) when (_localOtpBypassEnabled && IsProviderDeliveryFailure(ex))
        {
            var challenge = await ForceLocalBypassChallengeAsync(destination, ex, ct);
            _logger.LogWarning(
                ex,
                "Local OTP bypass activated for destination {Destination}. Using code {Code}.",
                destination,
                LocalBypassCode);
            return challenge;
        }
    }

    public async Task<AuthResultResponse> CompleteOtpAsync(
        string challengeId,
        string code,
        string destination,
        string? displayName,
        CancellationToken ct)
    {
        try
        {
            return await _inner.CompleteOtpAsync(challengeId, code, destination, displayName, ct);
        }
        catch (Exception ex) when (IsTransientOtpReadFailure(ex))
        {
            _logger.LogWarning(
                ex,
                "Transient OTP verification failure detected for challenge {ChallengeId}. Retrying once.",
                challengeId);

            await Task.Delay(150, ct);
            return await _inner.CompleteOtpAsync(challengeId, code, destination, displayName, ct);
        }
    }

    private async Task<OtpChallengeResult> ForceLocalBypassChallengeAsync(
        string destination,
        Exception originalException,
        CancellationToken ct)
    {
        if (_otpChallengeTable is null)
            throw originalException;

        await _otpChallengeTable.CreateIfNotExistsAsync(ct);

        var normalized = NormalizeDestination(destination);
        var filter = $"Destination eq '{normalized.Replace("'", "''", StringComparison.Ordinal)}'";
        TableEntity? latest = null;

        await foreach (var entity in _otpChallengeTable.QueryAsync<TableEntity>(filter: filter, cancellationToken: ct))
        {
            if (latest is null || GetCreatedAt(entity) > GetCreatedAt(latest))
                latest = entity;
        }

        if (latest is null)
            throw originalException;

        latest["CodeHash"] = ComputeOtpHash(LocalBypassCode, _otpHashSalt);
        latest["AttemptCount"] = 0;
        latest["IsConsumed"] = false;
        latest["VerificationToken"] = null;
        latest["VerificationTokenExpiresAt"] = null;

        await _otpChallengeTable.UpdateEntityAsync(latest, latest.ETag, TableUpdateMode.Replace, ct);

        return new OtpChallengeResult(
            latest.GetString("ChallengeId") ?? latest.RowKey,
            latest.GetDateTimeOffset("ExpiresAt") ?? DateTimeOffset.UtcNow.AddMinutes(10));
    }

    private static DateTimeOffset GetCreatedAt(TableEntity entity)
        => entity.GetDateTimeOffset("CreatedAt") ?? entity.Timestamp ?? DateTimeOffset.MinValue;

    private static string NormalizeDestination(string destination)
    {
        if (destination.Contains('@', StringComparison.Ordinal))
            return destination.Trim().ToLowerInvariant();

        var digits = new string(destination.Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
            return $"1{digits}";

        if (digits.Length == 11 && digits.StartsWith('1'))
            return digits;

        return digits;
    }

    private static string ComputeOtpHash(string code, string salt)
    {
        var method = typeof(OtpService).GetMethod(
            "HashCode",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Unable to locate OtpService.HashCode.");

        return (string)(method.Invoke(null, [code, salt]) ?? throw new InvalidOperationException("OTP hash computation returned null."));
    }

    private static bool IsProviderDeliveryFailure(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("Unexpected SMS provider error", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Unexpected email provider error", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Azure.Communication", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientOtpReadFailure(Exception ex)
    {
        var message = ex.Message?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(message)) return false;

        return message.Contains("otp challenge not found")
               || message.Contains("could not be reloaded after consume");
    }
}
