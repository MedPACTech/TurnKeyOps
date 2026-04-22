using System;

namespace MedInsights.Lib.Dtos;

public sealed record ChangePassword(
    string CurrentPassword,
    string NewPassword
);