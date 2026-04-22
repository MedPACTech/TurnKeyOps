using System.Security.Claims;
using AngleSharp.Io;
using MedInsights.Lib.Dtos;
using Microsoft.AspNetCore.Http;

namespace MedInsights.Services.Interfaces;

public interface IAuthService
{
    // Registration
    //Task<string> RegisterWithOtpAsync(RegisterDto dto, CancellationToken ct);
    Task<RegisterResponse> RegisterAsync(RegisterDto dto, HttpContext http, CancellationToken ct);
    Task<RegisterResponse> RegisterWithStripeAsync(RegisterWithStripeRequestDto dto, HttpContext http, CancellationToken ct);

    // Password login
    Task<LoginResponse> LoginAsync(LoginDto dto, HttpContext http, CancellationToken ct);

    // OTP login
    Task<StartOtpResponse> StartOtpAsync(StartOtpDto dto, HttpContext http, CancellationToken ct);
    Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpDto dto, HttpContext http, CancellationToken ct);

    // Email confirmation
    Task<SimpleMessageResponse> RequestEmailConfirmationAsync(RequestEmailConfirmationDto dto, HttpContext http, CancellationToken ct);
    Task<EmailConfirmationResponse> ConfirmEmailAsync(string email, string token, HttpContext http, CancellationToken ct);

    // Password reset + change
    Task<SimpleMessageResponse> RequestPasswordResetAsync(ResetPasswordRequestDto dto, HttpContext http, CancellationToken ct);
    Task<SimpleMessageResponse> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct);
    Task<SimpleMessageResponse> ChangePasswordAsync(ClaimsPrincipal user, ChangePasswordDto dto, CancellationToken ct);

    // Session revocation
    Task LogoutAsync(ClaimsPrincipal user, HttpRequest request, CancellationToken ct);
    Task LogoutAllAsync(ClaimsPrincipal user, CancellationToken ct);
}
