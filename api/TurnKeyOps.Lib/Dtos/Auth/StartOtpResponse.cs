using System;

namespace MedInsights.Lib.Dtos;

public sealed record StartOtpResponse(
    string Channel,              // "email" | "sms" | "choose"
    string DestinationMasked,    // e.g. a***@b.com or ********1234
    int ExpiresInSeconds,
    bool RequiresTermsAcceptance,
    string[]? AvailableChannels = null, // when Channel == "choose"
    string? DevCode = null              // only for dev if you want to echo
);