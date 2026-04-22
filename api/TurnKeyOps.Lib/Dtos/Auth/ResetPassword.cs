using System;

namespace MedInsights.Lib.Dtos;

public sealed record ResetPassword(
    string Email,
    string Token,
    string NewPassword
);