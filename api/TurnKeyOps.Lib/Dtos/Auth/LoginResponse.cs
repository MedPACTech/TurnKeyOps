using System;

namespace MedInsights.Lib.Dtos;

public sealed record LoginResponse(
    string Token,
    string[] Roles
);