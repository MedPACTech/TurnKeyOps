using System;

namespace MedInsights.Lib.Dtos;

public sealed class TermsAcceptanceDto
{
    public bool Accepted { get; set; }
    public string Version { get; set; } = "current";
}