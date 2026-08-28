using System.Reflection;
using MedInsights.Controllers;
using MedInsights.Lib.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace MedInsights.Authorization.Tests;

public sealed class ControllerAuthorizationInventoryTests
{
    [Fact]
    public void EveryProductionControllerActionDeclaresNamedAuthorizationOrAnonymousAccess()
    {
        var unsecured = new List<string>();
        var controllers = typeof(TenantProfileController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .ToList();

        foreach (var controller in controllers)
        {
            var classAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;
            var classPolicies = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Select(attribute => attribute.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .ToList();

            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                         .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any()))
            {
                if (classAnonymous || action.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null)
                    continue;

                var methodPolicies = action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                    .Select(attribute => attribute.Policy)
                    .Where(policy => !string.IsNullOrWhiteSpace(policy));

                if (!classPolicies.Concat(methodPolicies).Any())
                    unsecured.Add($"{controller.Name}.{action.Name}");
            }
        }

        Assert.True(unsecured.Count == 0, $"Actions without a named policy: {string.Join(", ", unsecured)}");
    }

    [Fact]
    public void DiagnosticAuthenticationEndpointsAreAbsentFromProductionAssembly()
    {
        var diagnosticControllers = typeof(TenantProfileController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => string.Equals(type.Name, "TestAuthController", StringComparison.Ordinal) ||
                           type.GetCustomAttributes<RouteAttribute>(inherit: true)
                               .Any(route => string.Equals(
                                   route.Template?.Trim('/'),
                                   "api/test-auth",
                                   StringComparison.OrdinalIgnoreCase)))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(diagnosticControllers);
    }

    [Theory]
    [InlineData(typeof(AuthSessionController), TurnKeyAuthorizationPolicies.AuthenticatedSession)]
    [InlineData(typeof(RolesController), TurnKeyAuthorizationPolicies.TenantAdmin)]
    [InlineData(typeof(TenantMembershipController), TurnKeyAuthorizationPolicies.TenantAdmin)]
    [InlineData(typeof(PlatformUserAdministrationController), TurnKeyAuthorizationPolicies.InternalAdmin)]
    [InlineData(typeof(BillingAdminController), TurnKeyAuthorizationPolicies.BillingAdmin)]
    [InlineData(typeof(TurnKeyOps.API.Controllers.JobsController), TurnKeyAuthorizationPolicies.TenantStaff)]
    public void SensitiveControllerUsesExpectedPolicy(Type controllerType, string expectedPolicy)
    {
        var policies = controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy)
            .ToList();

        Assert.Contains(expectedPolicy, policies);
    }
}
