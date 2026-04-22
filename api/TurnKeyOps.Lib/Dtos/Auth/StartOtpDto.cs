using System;

namespace MedInsights.Lib.Dtos;

public sealed class StartOtpDto
{
    // Identifier may be email OR phone; UI can also pass both but service should resolve.
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    // If user has both on file, UI can let them choose and pass "email" or "sms"
    public string? PreferredChannel { get; set; } // "email" | "sms" | null

    // Optional: when coming from register, UI may already have the user id
    public Guid? UserId { get; set; }
}