using MedInsights.Lib.Authorization;
using MedInsights.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MedInsights.API.Infrastructure;

// Additional per-user restrictions; existing tenant/role authorization still applies.
public sealed class UserModuleAccessFilter(UserModuleAccessService access) : IAsyncActionFilter
{
    public static string? ModuleFor(string controller) => controller switch {
        "People" or "TenantMembership" or "Invite" or "Roles" or "AdminContactAccess" => "users",
        "Customers" or "JobSites" => "contacts",
        "Jobs" => "jobs",
        "Calendar" or "Weather" => "calendar",
        "QuoteRequests" or "QuoteRequestAttachments" => "requests",
        "Estimates" or "QuoteEstimates" or "AdminEstimateDefaults" => "estimates",
        "Invoices" => "invoices",
        "Dashboard" => "dashboard",
        "Bob" or "BobActions" or "Chat" or "AIRealtime" => "bob",
        "AdminTenantSettings" or "TenantProfile" or "TenantOnboardingPolicy" or "ActivityLogs" => "settings",
        "Billing" or "BillingAdmin" or "TenantBillingAccount" or "TenantSubscription" or "TenantSeatEntitlement" or "TenantCreditBalance" or "TokenLedger" => "billing",
        _ => null
    };
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action ||
            context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null) { await next(); return; }
        // Authenticated invitation acceptance is independent of administrative invitation management.
        if (action.ControllerName == "Invite" && action.ActionName is "Redeem" or "GetAcceptanceContext") { await next(); return; }
        var module = action.ControllerName == "UserProfile" && action.ActionName is "Get" or "GetById" or "GetAsync" or "GetByIdAsync"
            ? "users" : ModuleFor(action.ControllerName);
        if (module is null) { await next(); return; }
        var write = !HttpMethods.IsGet(context.HttpContext.Request.Method) && !HttpMethods.IsHead(context.HttpContext.Request.Method);
        if (module == "users" && write && action.ControllerName != "People" && !await access.IsOwnerAsync(context.HttpContext.RequestAborted)) {
            context.Result = new ObjectResult(new { error = "Only an owner can change access roles and invitations." }) { StatusCode = 403 };
            return;
        }
        if (!UserModulePermissions.Allows(await access.GetAsync(context.HttpContext.RequestAborted), module, write)) {
            context.Result = new ObjectResult(new { error = "You do not have access to this module." }) { StatusCode = 403 };
            return;
        }
        await next();
    }
}
