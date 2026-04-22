using System;

namespace MedInsights.Lib.Dtos;

public sealed class RegisterDto
{
  public string? Email { get; set; } = string.Empty;
  public string? PhoneNumber { get; set; }
  public string? DisplayName { get; set; }
  public Guid? TenantId { get; set; }

}