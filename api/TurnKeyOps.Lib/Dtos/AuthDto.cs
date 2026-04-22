// using System;

// namespace MedInsights.Lib.Dtos;

// // =========================
// // Register
// // =========================

// // public sealed class RegisterDto
// // {
// //     public string? Email { get; set; }
// //     public string? PhoneNumber { get; set; }
// //     public string? DisplayName { get; set; }
// //     public Guid? TenantId { get; set; }
// // }

public sealed class RegisterWithStripeRequest
{
    // Stripe entrypoint (kept separate path as requested)
    public string SessionId { get; set; } = string.Empty;

    // Optional overrides (handy if the confirmation page allows edits)
    public string? EmailOverride { get; set; }
    public string? PhoneOverride { get; set; }
    public string? DisplayNameOverride { get; set; }
    public Guid? TenantId { get; set; }
}

// public sealed record RegisterResponse(
//     Guid UserId,
//     string? Email,
//     bool EmailOnFile,
//     bool EmailConfirmed,
//     string? PhoneNumber,
//     bool PhoneOnFile,
//     bool PhoneConfirmed,
//     string[] AvailableLoginChannels // e.g. ["email","sms"]
// );


// // =========================
// // Login (password)
// // =========================

// // public sealed class LoginDto
// // {
// //     public string Email { get; set; } = string.Empty;
// //     public string Password { get; set; } = string.Empty;
// // }

// // public sealed record LoginResponse(
// //     string Token,
// //     string[] Roles
// // );


// // =========================
// // OTP flow
// // =========================

// // public sealed class StartOtpDto
// // {
// //     // Identifier may be email OR phone; UI can also pass both but service should resolve.
// //     public string? Email { get; set; }
// //     public string? PhoneNumber { get; set; }

// //     // If user has both on file, UI can let them choose and pass "email" or "sms"
// //     public string? PreferredChannel { get; set; } // "email" | "sms" | null

// //     // Optional: when coming from register, UI may already have the user id
// //     public Guid? UserId { get; set; }
// // }

// // public sealed record StartOtpResponse(
// //     string Channel,              // "email" | "sms" | "choose"
// //     string DestinationMasked,    // e.g. a***@b.com or ********1234
// //     int ExpiresInSeconds,
// //     bool RequiresTermsAcceptance,
// //     string[]? AvailableChannels = null, // when Channel == "choose"
// //     string? DevCode = null              // only for dev if you want to echo
// // );

// public sealed class VerifyOtpDto
// {
//     // Must identify which account/channel we’re verifying
//     public string? Email { get; set; }
//     public string? PhoneNumber { get; set; }
//     public string Channel { get; set; } = string.Empty; // "email" | "sms"

//     // 6-digit code
//     public string Code { get; set; } = string.Empty;

//     // Only required if server says RequiresTermsAcceptance=true
//     public TermsAcceptanceDto? Terms { get; set; }
// }

// public sealed class TermsAcceptanceDto
// {
//     public bool Accepted { get; set; }
//     public string Version { get; set; } = "current";
// }

// // Verify response supports:
// // - success -> returns token + roles
// // - terms required -> returns RequiresTermsAcceptance=true with no token
// public sealed record VerifyOtpResponse(
//     bool Success,
//     bool RequiresTermsAcceptance,
//     string? Token = null,
//     string[]? Roles = null,
//     string? Message = null
// );


// // =========================
// // Email confirmation
// // =========================

// public sealed record EmailDto(string Email);

public sealed record RequestEmailConfirmationDto(
    string Email
    // If you follow your prior pattern: return reset token after email confirm (optional)

);


// // =========================
// // Password reset / change
// // =========================

public sealed record ResetPasswordRequestDto(string Email);

public sealed record ResetPasswordDto(
    string Email,
    string Token,
    string NewPassword
);

public sealed record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword
);

public sealed record SimpleMessageResponse(string Message);
