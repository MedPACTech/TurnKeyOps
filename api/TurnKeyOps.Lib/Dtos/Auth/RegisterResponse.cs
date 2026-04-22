using System;

namespace MedInsights.Lib.Dtos;

public sealed record RegisterResponse(
    Guid UserId,
    string? Email,
    bool EmailOnFile,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneOnFile,
    bool PhoneConfirmed,
    string[] AvailableLoginChannels // e.g. ["email","sms"]
);