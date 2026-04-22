// using AngleSharp.Dom;
// using IBeam.Communications.Abstractions;
// using IBeam.Identity.Services.Auth;
// using MedInsights.Lib.Configurations;
// using MedInsights.Lib.Dtos;
// using MedInsights.Services.Interfaces;
// using MedPACTech.Identity;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.AspNetCore.WebUtilities;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Caching.Distributed;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Options;
// using System.IdentityModel.Tokens.Jwt;
// using System.Security.Claims;
// using System.Security.Cryptography;
// using System.Text;
// using System.Text.Json;

// namespace MedInsights.Services;

// public sealed class AuthService : IAuthService
// {
//    // private readonly UserManager<ApplicationUser> _users;
//     private readonly ITemplatedEmailService _email; // ✅ templated-only
//     private readonly ISmsService _sms;
//     private readonly IConfiguration _cfg;
//     private readonly IHostEnvironment _env;
//     //private readonly ITokenService _tokens;
//     //private readonly ITokenRevocationStore _revocations;
//     //private readonly AppIdentityDbContext _identityDb;
//     private readonly IDistributedCache _cache;
//     private readonly string _applicationHost;
//     private readonly IIdentityOtpAuthService _otpAuthService;

//     // OTP policy (tune via config later if desired)
//     private const int OtpDigits = 6;
//     private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
//     private const int MaxOtpAttempts = 5;

//     public AuthService(
//         IOptions<SystemSettings> systemSettings,
//         //UserManager<ApplicationUser> users,
//         ITemplatedEmailService email,      // ✅ templated-only
//         ISmsService sms,
//         IConfiguration cfg,
//         IHostEnvironment env,
//         //ITokenService tokens,
//         ///ITokenRevocationStore revocations,
//         //AppIdentityDbContext identityDb,
//         IDistributedCache cache,
//         IIdentityOtpAuthService otpAuthService) 
//     {
//         //_users = users;
//         _email = email;
//         _sms = sms;
//         _cfg = cfg;
//         _env = env;
//        //_tokens = tokens;
//         //_revocations = revocations;
//         //_identityDb = identityDb;
//         _cache = cache;
//         _applicationHost = systemSettings.Value.ApplicationHost;
//         _otpAuthService = otpAuthService;
//     }

//     // =========================
//     // Registration
//     // =========================

//     // New OTP-based registration method
//     // public async Task<string> RegisterWithOtpAsync(RegisterDto dto, CancellationToken ct)
//     // {
//     //     var destination = NormalizeEmail(dto.Email) ?? NormalizePhone(dto.PhoneNumber);
//     //     // Call IBeam's OtpAuthService for registration
//     //     var registrationResponse = await _otpAuthService.RegisterUserViaOtpAsync(destination, dto.TenantId, ct);

//     //     // Map IBeam's response to our RegisterResponse
//     //     return registrationResponse.ChallengeId;
//     // }

//     public async Task<RegisterResponse> RegisterAsync(RegisterDto dto, HttpContext http, CancellationToken ct)
//     {
//         var email = NormalizeEmail(dto.Email);
//         var phone = NormalizePhone(dto.PhoneNumber);

//         if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
//             throw new InvalidOperationException("Email or PhoneNumber is required.");

//         if (dto.TenantId is null || dto.TenantId == Guid.Empty)
//             dto.TenantId = Guid.NewGuid();

//         // Find existing by email or phone
//         ApplicationUser? user = null;

//         if (!string.IsNullOrWhiteSpace(email))
//             //user = await _users.FindByEmailAsync(email);

//         if (user is null && !string.IsNullOrWhiteSpace(phone))
//             //user = _users.Users.FirstOrDefault(u => u.PhoneNumber == phone);

//         // Create if missing
//         if (user is null)
//         {
//             user = new ApplicationUser
//             {
//                 Id = Guid.NewGuid(),
//                 TenantId = dto.TenantId,
//                 Email = email,
//                 UserName = email ?? phone ?? Guid.NewGuid().ToString("N"),
//                 DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? (email ?? phone!) : dto.DisplayName!.Trim(),
//                 PhoneNumber = phone
//             };

//         //    var create = await _users.CreateAsync(user);
//         //    if (!create.Succeeded)
//         //        throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

//             await EnsureUserProvisioningAsync(user, ct);
//         }
//         else
//         {
//             // Attach missing fields
//             var dirty = false;

//             if (!string.IsNullOrWhiteSpace(email) && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
//             {
//                 user.Email = email;
//                 user.UserName ??= email;
//                 dirty = true;
//             }

//             if (!string.IsNullOrWhiteSpace(phone) && !string.Equals(user.PhoneNumber, phone, StringComparison.Ordinal))
//             {
//                 user.PhoneNumber = phone;
//                 dirty = true;
//             }

//             if (!string.IsNullOrWhiteSpace(dto.DisplayName))
//             {
//                 var dn = dto.DisplayName.Trim();
//                 if (!string.Equals(user.DisplayName, dn, StringComparison.Ordinal))
//                 {
//                     user.DisplayName = dn;
//                     dirty = true;
//                 }
//             }

//             // Only set TenantId if missing
//             if (user.TenantId is null || user.TenantId == Guid.Empty)
//             {
//                 user.TenantId = dto.TenantId;
//                 dirty = true;
//             }

//             if (dirty)
//                 //await _users.UpdateAsync(user);

//             await EnsureUserProvisioningAsync(user, ct);
//         }

//         return BuildRegisterResponse(user);
//     }

//     public Task<RegisterResponse> RegisterWithStripeAsync(RegisterWithStripeRequestDto dto, HttpContext http, CancellationToken ct)
//     {
//         // Stripe is intentionally out for the moment.
//         throw new NotSupportedException("Stripe registration is not implemented yet. Use /api/auth/register for now.");
//     }

//     // =========================
//     // Password login
//     // =========================

//     public async Task<LoginResponse> LoginAsync(LoginDto dto, HttpContext http, CancellationToken ct)
//     {
//         var email = NormalizeEmail(dto.Email);
//         if (string.IsNullOrWhiteSpace(email)) throw new UnauthorizedAccessException();

//        // var user = await _users.FindByEmailAsync(email);
//         //if (user is null) throw new UnauthorizedAccessException();

//         //if (!user.EmailConfirmed)
//         //    throw new UnauthorizedAccessException("Email not confirmed.");

//         //var valid = await _users.CheckPasswordAsync(user, dto.Password);
//         //if (!valid) throw new UnauthorizedAccessException();

//         ////var token = await _tokens.CreateAsync(user);
//         ////await WriteSessionAsync(user, token, http, ct);

//         //var roles = await _users.GetRolesAsync(user);
//         return new LoginResponse(null, null);
//     }

//     // =========================
//     // OTP flow
//     // =========================

//     public async Task<StartOtpResponse> StartOtpAsync(StartOtpDto dto, HttpContext http, CancellationToken ct)
//     {
//         // Identify user
//         ApplicationUser? user = null;

//         // if (dto.UserId is not null && dto.UserId != Guid.Empty)
//         //     user = await _users.FindByIdAsync(dto.UserId.Value.ToString());

//         // var email = NormalizeEmail(dto.Email);
//         // var phone = NormalizePhone(dto.PhoneNumber);

//         // if (user is null && !string.IsNullOrWhiteSpace(email))
//         //     user = await _users.FindByEmailAsync(email);

//         // if (user is null && !string.IsNullOrWhiteSpace(phone))
//         //     user = _users.Users.FirstOrDefault(u => u.PhoneNumber == phone);

//         // If no user, return generic response (don’t leak existence)
//         // if (user is null)
//         // {
//         //     //if (!string.IsNullOrWhiteSpace(email))
//         //     //    return new StartOtpResponse("email", MaskEmail(email), (int)OtpLifetime.TotalSeconds, RequiresTermsAcceptance: true, AvailableChannels: null, DevCode: _env.IsDevelopment() ? "000000" : null);

//         //     if (!string.IsNullOrWhiteSpace(phone))
//         //         return new StartOtpResponse("sms", MaskPhone(phone), (int)OtpLifetime.TotalSeconds, RequiresTermsAcceptance: true, AvailableChannels: null, DevCode: _env.IsDevelopment() ? "000000" : null);

//         //     throw new InvalidOperationException("Email or PhoneNumber is required.");
//         // }

//          var hasEmail = !string.IsNullOrWhiteSpace(dto.Email);
//          var hasPhone = !string.IsNullOrWhiteSpace(dto.PhoneNumber);

//         // if (!hasEmail && !hasPhone)
//         //     throw new InvalidOperationException("User has no email or phone on file.");

//         // Determine channel
//         // var preferred = (dto.PreferredChannel ?? string.Empty).Trim().ToLowerInvariant();

//         // if (hasEmail && hasPhone && string.IsNullOrWhiteSpace(preferred))
//         // {
//         //     return new StartOtpResponse(
//         //         Channel: "choose",
//         //         DestinationMasked: string.Empty,
//         //         ExpiresInSeconds: (int)OtpLifetime.TotalSeconds,
//         //         RequiresTermsAcceptance: RequiresTerms(user),
//         //         AvailableChannels: new[] { "email", "sms" },
//         //         DevCode: null);
//         // }

//         string channel;
//         string destination;

//         //if (preferred == "sms")
//        // {
//             if (!hasPhone) throw new InvalidOperationException("SMS not available for this user.");
//             channel = "sms";
//             destination = dto.PhoneNumber!;
//        // }
//        // else
//        // {
//             // if (!hasEmail) throw new InvalidOperationException("Email not available for this user.");
//             // channel = "email";
//             // destination = user.Email!;
//        // }

//         // Create challenge
//         var code = GenerateOtpCode();
//         var now = DateTimeOffset.UtcNow;

//         // var challenge = new OtpChallenge
//         // {
//         //     UserId = user.Id,
//         //     Channel = channel,
//         //     Destination = destination,
//         //     CodeHash = HashOtp(code),
//         //     ExpiresAt = now.Add(OtpLifetime),
//         //     Attempts = 0,
//         //     Used = false
//         // };

//         // await SaveChallengeAsync(challenge, ct);

//         // Send
//         // if (channel == "email")
//         // {

//         //     //add email to a empty list
//         //     var emailList = new List<string> { user.Email!, destination };

//         //     await _email.SendTemplatedEmailAsync(
//         //         to: emailList,
//         //         subject: "Your verification code",
//         //         templateName: "OtpCodeEmail.html",
//         //         model: new object[] { code },
//         //         options: null,
//         //         ct: ct);

//         //     return new StartOtpResponse(channel, MaskEmail(destination), (int)OtpLifetime.TotalSeconds, RequiresTerms(user),
//         //         AvailableChannels: null,
//         //         DevCode: _env.IsDevelopment() ? code : null);
//         // }

//         var smsMsg = new SmsMessage { Body = $"Your verification code is: {code}" };
//         smsMsg.To.Add(destination);

//         await _sms.SendAsync(smsMsg, options: null, ct);

//         return new StartOtpResponse(channel, MaskPhone(destination), (int)OtpLifetime.TotalSeconds, RequiresTerms(user),
//             AvailableChannels: null,
//             DevCode: _env.IsDevelopment() ? code : null);
//     }

//     public async Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpDto dto, HttpContext http, CancellationToken ct)
//     {
//         var channel = (dto.Channel ?? string.Empty).Trim().ToLowerInvariant();
//         if (channel != "email" && channel != "sms")
//             throw new InvalidOperationException("Channel must be 'email' or 'sms'.");

//         var email = NormalizeEmail(dto.Email);
//         var phone = NormalizePhone(dto.PhoneNumber);

//         string destination;
//         if (channel == "email")
//         {
//             destination = !string.IsNullOrWhiteSpace(email) ? email : throw new InvalidOperationException("Email is required for email OTP.");
//         }
//         else
//         {
//             destination = !string.IsNullOrWhiteSpace(phone) ? phone : throw new InvalidOperationException("PhoneNumber is required for sms OTP.");
//         }

//         var key = BuildOtpKey(channel, destination);
//         var ch = await LoadChallengeAsync(key, ct);

//         if (ch is null || ch.Used || ch.ExpiresAt <= DateTimeOffset.UtcNow)
//             return new VerifyOtpResponse(Success: false, RequiresTermsAcceptance: false, Message: "Invalid or expired code.");

//         if (ch.Attempts >= MaxOtpAttempts)
//             return new VerifyOtpResponse(Success: false, RequiresTermsAcceptance: false, Message: "Too many attempts.");

//         if (!VerifyOtp(dto.Code, ch.CodeHash))
//         {
//             ch.Attempts++;
//             await SaveChallengeAsync(ch, ct);
//             return new VerifyOtpResponse(Success: false, RequiresTermsAcceptance: false, Message: "Invalid code.");
//         }

//         // Mark used
//         ch.Used = true;
//         await SaveChallengeAsync(ch, ct);

//        // var user = await _users.FindByIdAsync(ch.UserId.ToString());
//        // if (user is null)
//             return new VerifyOtpResponse(Success: false, RequiresTermsAcceptance: false, Message: "Invalid user.");

//         // First verification of that method -> confirm channel
//       //  var firstConfirm = false;

//       //  if (channel == "email" && !user.EmailConfirmed)
//       //  {
//       //      user.EmailConfirmed = true;
//       //      firstConfirm = true;
//       //  }

//       //  if (channel == "sms")
//       //  {
//       //      if (!user.PhoneNumberConfirmed)
//       //      {
//       //          user.PhoneNumberConfirmed = true;
//       //          firstConfirm = true;
//       //      }
//       //  }

//       //  // Terms gating
//       //  var needsTerms = RequiresTerms(user);

//       //  if (needsTerms)
//       //  {
//       //      if (dto.Terms is null || dto.Terms.Accepted != true)
//       //      {
//       //          if (firstConfirm) await _users.UpdateAsync(user);

//       //          return new VerifyOtpResponse(
//       //              Success: false,
//       //              RequiresTermsAcceptance: true,
//       //              Message: "Terms acceptance required.");
//       //      }

//       //      //user.TermsAcceptedAtUtc = DateTimeOffset.UtcNow;
//       //      //user.TermsVersion = dto.Terms.Version;
//       //  }

//       //  if (firstConfirm || needsTerms)
//       //      await _users.UpdateAsync(user);

//       //  // Issue token + session
//       ////  var token = await _tokens.CreateAsync(user);
//       //  //await WriteSessionAsync(user, token, http, ct);

//       //  var roles = await _users.GetRolesAsync(user);
//       //  return new VerifyOtpResponse(Success: true, RequiresTermsAcceptance: false, Token: null, Roles: roles.ToArray(), Message: "OK");
//     }

//     // =========================
//     // Email confirmation
//     // =========================
//     // Email confirmation
//     // =========================

//     public async Task<SimpleMessageResponse> RequestEmailConfirmationAsync(RequestEmailConfirmationDto dto, HttpContext http, CancellationToken ct)
//     {
//         //var email = NormalizeEmail(dto.Email);
//         //if (string.IsNullOrWhiteSpace(email)) return new SimpleMessageResponse("OK");

//         //var user = await _users.FindByEmailAsync(email);
//         //if (user is null) return new SimpleMessageResponse("OK");

//         //var c = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
//         //    await _users.GenerateEmailConfirmationTokenAsync(user)));

//         //string? r = null;
//         //if (!await _users.HasPasswordAsync(user))
//         //{
//         //    r = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
//         //        await _users.GeneratePasswordResetTokenAsync(user)));
//         //}

//         //var link = BuildLink(http, "confirm-email", new Dictionary<string, string?>
//         //{
//         //    ["email"] = email,
//         //    ["token"] = c,
//         //    ["r"] = r
//         //});

//         ////add email to a empty list
//         //var emailList = new List<string> { user.Email!, email };
       
//         //await _email.SendTemplatedEmailAsync(
//         //    to: emailList,
//         //    subject: "Confirm your email",
//         //    templateName: "QurviaEmailConfirm.html",
//         //    model: new object[] { link },
//         //    options: null,
//         //    ct: ct);

//         return new SimpleMessageResponse("OK");
//     }

//     public async Task<EmailConfirmationResponse> ConfirmEmailAsync(string email, string token, HttpContext http, CancellationToken ct)
//     {
//         //var normEmail = NormalizeEmail(email);
//         //var user = await _users.FindByEmailAsync(normEmail);
//         //if (user is null) throw new InvalidOperationException("Invalid user.");

//         //var confirmRaw = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
//         //var confirmResult = await _users.ConfirmEmailAsync(user, confirmRaw);

//         //if (!confirmResult.Succeeded)
//         //    throw new InvalidOperationException(string.Join("; ", confirmResult.Errors.Select(e => e.Description)));

//         //if (!await _users.HasPasswordAsync(user))
//         //{
//         //    var resetToken = await _users.GeneratePasswordResetTokenAsync(user);
//         //    var resetEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));

//         //    return new EmailConfirmationResponse(
//         //        Message: "Email confirmed. Please set your password.",
//         //        ResetToken: resetEncoded);
//         //}

//         return new EmailConfirmationResponse("Email confirmed.");
//     }

//     // =========================
//     // Password reset / change
//     // =========================

//     public async Task<SimpleMessageResponse> RequestPasswordResetAsync(ResetPasswordRequestDto dto, HttpContext http, CancellationToken ct)
//     {
//         //var email = NormalizeEmail(dto.Email);
//         //if (string.IsNullOrWhiteSpace(email)) return new SimpleMessageResponse("OK");

//         //var user = await _users.FindByEmailAsync(email);
//         //if (user is null) return new SimpleMessageResponse("OK");

//         //var token = await _users.GeneratePasswordResetTokenAsync(user);
//         //var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

//         //var link = BuildLink(http, "passwordReset", new Dictionary<string, string?>
//         //{
//         //    ["email"] = email,
//         //    ["token"] = encoded
//         //});

//         //await _email.SendTemplatedEmailAsync(
//         //    to: new List<string> { user.Email! },
//         //    subject: "Reset your password",
//         //    templateName: "QurviaPasswordReset.html",
//         //    model: new object[] { link },
//         //    options: null,
//         //    ct: ct);

//         return new SimpleMessageResponse("OK");
//     }

//     public async Task<SimpleMessageResponse> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct)
//     {
//         //var email = NormalizeEmail(dto.Email);
//         //var user = await _users.FindByEmailAsync(email);
//         //if (user is null) throw new InvalidOperationException("Invalid user.");

//         //var resetRaw = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Token));
//         //var result = await _users.ResetPasswordAsync(user, resetRaw, dto.NewPassword);

//         //if (!result.Succeeded)
//         //    throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

//         return new SimpleMessageResponse("Password has been set successfully.");
//     }

//     public async Task<SimpleMessageResponse> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordDto dto, CancellationToken ct)
//     {
//         //var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
//         //if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException();

//         //var user = await _users.FindByIdAsync(userId);
//         //if (user is null) throw new UnauthorizedAccessException();

//         //var res = await _users.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
//         //if (!res.Succeeded)
//         //    throw new InvalidOperationException(string.Join("; ", res.Errors.Select(e => e.Description)));

//         return new SimpleMessageResponse("Password changed.");
//     }

//     // =========================
//     // Session revocation
//     // =========================

//     public async Task LogoutAsync(ClaimsPrincipal principal, HttpRequest request, CancellationToken ct)
//     {
//         //var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
//         //          ?? principal.FindFirst("jti")?.Value;

//         //if (string.IsNullOrWhiteSpace(jti))
//         //    throw new InvalidOperationException("Missing jti.");

//         //var session = await _identityDb.UserSessions.FindAsync([jti], ct);
//         //if (session is not null && !session.Revoked)
//         //{
//         //    session.Revoked = true;
//         //    session.RevokedAt = DateTimeOffset.UtcNow;
//         //    session.RevokedBy = principal.Identity?.Name ?? "self";
//         //    session.RevokeReason = "User logout";
//         //    await _identityDb.SaveChangesAsync(ct);
//         //}

//         //var raw = request.Headers.Authorization.ToString()
//         //    .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

//         //if (!string.IsNullOrWhiteSpace(raw))
//         //{
//         //    var jwt = new JwtSecurityTokenHandler().ReadJwtToken(raw);
//         //    await _revocations.RevokeAsync(jti, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero), ct);
//         //}
//     }

//     public async Task LogoutAllAsync(ClaimsPrincipal principal, CancellationToken ct)
//     {
//         //var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
//         //            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

//         //if (string.IsNullOrWhiteSpace(userId))
//         //    throw new InvalidOperationException("No user id.");

//         //var user = await _users.FindByIdAsync(userId);
//         //if (user is null) throw new KeyNotFoundException();

//         //await _users.UpdateSecurityStampAsync(user);

//         //var now = DateTimeOffset.UtcNow;
//         //var sessions = _identityDb.UserSessions
//         //    .Where(s => s.UserId == user.Id && !s.Revoked && s.ExpiresAt > now);

//         //foreach (var s in sessions)
//         //{
//         //    s.Revoked = true;
//         //    s.RevokedAt = now;
//         //    s.RevokedBy = "logout-all";
//         //    s.RevokeReason = "Security stamp rotated";
//         //    await _revocations.RevokeAsync(s.Jti, s.ExpiresAt, ct);
//         //}

//         //await _identityDb.SaveChangesAsync(ct);
//     }

//     // =========================
//     // Helpers
//     // =========================

//     private RegisterResponse BuildRegisterResponse(ApplicationUser user)
//     {
//         var emailOnFile = !string.IsNullOrWhiteSpace(user.Email);
//         var phoneOnFile = !string.IsNullOrWhiteSpace(user.PhoneNumber);

//         var channels = new List<string>(2);
//         if (emailOnFile) channels.Add("email");
//         if (phoneOnFile) channels.Add("sms");

//         return new RegisterResponse(
//             UserId: user.Id,
//             Email: user.Email,
//             EmailOnFile: emailOnFile,
//             EmailConfirmed: user.EmailConfirmed,
//             PhoneNumber: user.PhoneNumber,
//             PhoneOnFile: phoneOnFile,
//             PhoneConfirmed: user.PhoneNumberConfirmed,
//             AvailableLoginChannels: channels.ToArray()
//         );
//     }

//     private async Task EnsureUserProvisioningAsync(ApplicationUser user, CancellationToken ct)
//     {
//         //if (await _identityDb.UserPreferences.AnyAsync(p => p.UserId == user.Id, ct)) return;

//         //_identityDb.UserPreferences.Add(new UserPreferences
//         //{
//         //    UserId = user.Id,
//         //    DisplayName = user.DisplayName ?? user.Email ?? user.PhoneNumber,
//         //    Theme = ThemePreference.System
//         //});

//         //await _identityDb.SaveChangesAsync(ct);
//     }

//     private bool RequiresTerms(ApplicationUser user)
//         =>  false;//user.TermsAcceptedAtUtc is null;

//     private async Task WriteSessionAsync(ApplicationUser user, string token, HttpContext http, CancellationToken ct)
//     {
//         //var handler = new JwtSecurityTokenHandler();
//         //var jwt = handler.ReadJwtToken(token);

//         //var jti = jwt.Id;
//         //var exp = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);

//         //var iat = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat)?.Value;
//         //var issuedAt = iat is not null
//         //    ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(iat))
//         //    : DateTimeOffset.UtcNow;

//         //var session = new UserSession
//         //{
//         //    Jti = jti,
//         //    UserId = user.Id,
//         //    Email = user.Email,
//         //    Tenant = user.TenantId?.ToString(),
//         //    IssuedAt = issuedAt,
//         //    ExpiresAt = exp,
//         //    SecurityStampAtIssue = user.SecurityStamp,
//         //    UserAgent = http.Request.Headers.UserAgent.ToString(),
//         //    IpAddress = http.Connection.RemoteIpAddress?.ToString(),
//         //    LastSeenAt = issuedAt
//         //};

//         //_identityDb.UserSessions.Add(session);
//         //await _identityDb.SaveChangesAsync(ct);
//     }

//     private string BuildLink(HttpContext http, string path, IDictionary<string, string?> qs)
//     {
//         string? baseUrl;

//         if (_env.IsDevelopment())
//         {
//             baseUrl = _cfg["SystemSettings:ApplicationHost"];
//             if (string.IsNullOrWhiteSpace(baseUrl))
//                 baseUrl = "https://localhost:5173";
//         }
//         else
//         {
//             baseUrl = _cfg["Frontend:BaseUrl"];
//             if (string.IsNullOrWhiteSpace(baseUrl))
//                 baseUrl = _applicationHost;
//         }

//         var uri = new Uri(new Uri(baseUrl, UriKind.Absolute), path);
//         return QueryHelpers.AddQueryString(uri.ToString(), qs!);
//     }

//     // ----- OTP storage (cache) -----

//     private sealed class OtpChallenge
//     {
//         public Guid UserId { get; set; }
//         public string Channel { get; set; } = string.Empty;      // "email" | "sms"
//         public string Destination { get; set; } = string.Empty;  // email or phone
//         public string CodeHash { get; set; } = string.Empty;
//         public DateTimeOffset ExpiresAt { get; set; }
//         public int Attempts { get; set; }
//         public bool Used { get; set; }
//     }

//     private static string BuildOtpKey(string channel, string destination)
//         => $"otp:{channel}:{destination}";

//     private async Task SaveChallengeAsync(OtpChallenge ch, CancellationToken ct)
//     {
//         var key = BuildOtpKey(ch.Channel, ch.Destination);
//         var json = JsonSerializer.Serialize(ch);
//         var ttl = ch.ExpiresAt - DateTimeOffset.UtcNow;
//         if (ttl <= TimeSpan.Zero) ttl = TimeSpan.FromSeconds(1);

//         await _cache.SetStringAsync(
//             key,
//             json,
//             new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
//             ct);
//     }

//     private async Task<OtpChallenge?> LoadChallengeAsync(string key, CancellationToken ct)
//     {
//         var json = await _cache.GetStringAsync(key, ct);
//         return json is null ? null : JsonSerializer.Deserialize<OtpChallenge>(json);
//     }

//     private static string GenerateOtpCode()
//         => RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, OtpDigits)).ToString($"D{OtpDigits}");

//     private static string HashOtp(string code)
//     {
//         var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
//         return Convert.ToBase64String(bytes);
//     }

//     private static bool VerifyOtp(string code, string codeHash)
//         => HashOtp(code) == codeHash;

//     // ----- Normalization + masking -----

//     private static string? NormalizeEmail(string? email)
//         => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

//     private static string? NormalizePhone(string? phone)
//         => string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(); // TODO: E.164 normalize

//     private static string MaskEmail(string email)
//     {
//         var at = email.IndexOf('@');
//         if (at <= 1) return "***" + (at >= 0 ? email[at..] : "");
//         return email[0] + "***" + email[(at - 1)..];
//     }

//     private static string MaskPhone(string phone)
//     {
//         if (phone.Length <= 4) return "****";
//         return new string('*', Math.Max(0, phone.Length - 4)) + phone[^4..];
//     }
// }


