using System;

namespace MedInsights.Lib.Dtos;

// Verify response supports:
// - success -> returns token + roles
// - terms required -> returns RequiresTermsAcceptance=true with no token
public sealed record VerifyOtpResponse(
    bool Success,
    bool RequiresTermsAcceptance,
    string? Token = null,
    string[]? Roles = null,
    string? Message = null
);
