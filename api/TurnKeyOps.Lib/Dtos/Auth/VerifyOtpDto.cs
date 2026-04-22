using System;

namespace MedInsights.Lib.Dtos;

public sealed class VerifyOtpDto
{
    // Must identify which account/channel we’re verifying
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string Channel { get; set; } = string.Empty; // "email" | "sms"

    // 6-digit code
    public string Code { get; set; } = string.Empty;

    // Only required if server says RequiresTermsAcceptance=true
    public TermsAcceptanceDto? Terms { get; set; }
}
