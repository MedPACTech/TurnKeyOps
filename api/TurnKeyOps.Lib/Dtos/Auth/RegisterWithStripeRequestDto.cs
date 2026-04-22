using System;

namespace MedInsights.Lib.Dtos;

public sealed class RegisterWithStripeRequestDto
{
  // Stripe entrypoint (kept separate path as requested)
  public string SessionId { get; set; } = string.Empty;

  // Optional overrides (handy if the confirmation page allows edits)
  public string? EmailOverride { get; set; }
  public string? PhoneOverride { get; set; }
  public string? DisplayNameOverride { get; set; }
  public Guid? TenantId { get; set; }

    // Stripe data
  public string? StripeCustomerId { get; set; }
  public string? StripeSubscriptionId { get; set; }
  public string? StripePromoCode { get; set; }
}