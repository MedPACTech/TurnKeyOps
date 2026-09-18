using IBeam.Identity.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace MedInsights.API.Infrastructure;

/// <summary>
/// OTP sign-in must be reachable before a session exists. Keep the host's
/// authenticated fallback policy for every other packaged identity endpoint.
/// </summary>
public sealed class IdentityOtpAnonymousConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        if (action.Controller.ControllerType != typeof(AuthController) ||
            action.ActionMethod.Name is not ("StartOtp" or "CompleteOtp"))
            return;

        foreach (var selector in action.Selectors)
            selector.EndpointMetadata.Add(new AllowAnonymousAttribute());
    }
}
