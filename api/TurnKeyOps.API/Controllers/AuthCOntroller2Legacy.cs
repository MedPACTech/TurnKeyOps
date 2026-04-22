// using AngleSharp.Io;
// using MedInsights.Controllers;
// using MedInsights.Lib.Dtos;
// using MedInsights.Services.Interfaces;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;

// [ApiController]
// [Route("api/auth")]
// public class AuthController2 : ApiControllerBase
// {
//     private readonly IAuthService _auth;

//     public AuthController2(IAuthService auth) => _auth = auth;

//     [HttpPost("registerwithstripe")]
//     [AllowAnonymous]
//     public Task<RegisterResponse> RegisterWithStripe(RegisterWithStripeRequestDto dto, CancellationToken ct)
//     => _auth.RegisterWithStripeAsync(dto, HttpContext, ct);

//     // [HttpPost("register")]
//     // [AllowAnonymous]
//     // public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
//     //     => Ok(await _auth.RegisterWithOtpAsync(dto, ct));

//     // [HttpPost("login")]
//     // [AllowAnonymous]
//     // public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
//     //     => Ok(await _auth.LoginAsync(dto, HttpContext, ct));

//     // // OTP-first flow
//     // [HttpPost("start-otp")]
//     // [AllowAnonymous]
//     // public async Task<IActionResult> StartOtp([FromBody] StartOtpDto dto, CancellationToken ct)
//     //     => Ok(await _auth.StartOtpAsync(dto, HttpContext, ct));

//     // [HttpPost("verify-otp")]
//     // [AllowAnonymous]
//     // public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto, CancellationToken ct)
//     //     => Ok(await _auth.VerifyOtpAsync(dto, HttpContext, ct));

//     // // Email confirmation + password flows (still auth)
//     // [HttpPost("request-email-confirmation")]
//     // [AllowAnonymous]
//     // public async Task<IActionResult> RequestEmailConfirmation([FromBody] RequestEmailConfirmationDto dto, CancellationToken ct)
//     //     => Ok(await _auth.RequestEmailConfirmationAsync(dto, HttpContext, ct));

//     // [HttpGet("confirm-email")]
//     // [AllowAnonymous]
//     // public async Task<IActionResult> ConfirmEmail([FromQuery] string email, [FromQuery] string token, CancellationToken ct)
//     //     => Ok(await _auth.ConfirmEmailAsync(email, token, HttpContext, ct));

//     // [HttpPost("request-password-reset")]
//     // [AllowAnonymous]
//     // public async Task<IActionResult> RequestPasswordReset([FromBody] ResetPasswordRequestDto dto, CancellationToken ct)
//     //     => Ok(await _auth.RequestPasswordResetAsync(dto, HttpContext, ct));

//     // [HttpPost("reset-password")]
//     // [AllowAnonymous]
//     // public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto, CancellationToken ct)
//     //     => Ok(await _auth.ResetPasswordAsync(dto, ct));

//     // [HttpPost("change-password")]
//     // [Authorize]
//     // public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct)
//     //     => Ok(await _auth.ChangePasswordAsync(User, dto, ct));

//     // // Session revocation
//     // [Authorize]
//     // [HttpPost("logout")]
//     // public async Task<IActionResult> Logout(CancellationToken ct)
//     // {
//     //     await _auth.LogoutAsync(User, Request, ct);
//     //     return Ok();
//     // }

//     // [Authorize]
//     // [HttpPost("logout-all")]
//     // public async Task<IActionResult> LogoutAll(CancellationToken ct)
//     // {
//     //     await _auth.LogoutAllAsync(User, ct);
//     //     return Ok();
//     // }

//     // Dev-only helpers can stay for now, but ideally move later
// }
