using System;

namespace MedInsights.Lib.Dtos;

public sealed record EmailConfirmationResponse(
    string Message,
    // If you follow your prior pattern: return reset token after email confirm (optional)
    string? ResetToken = null
);