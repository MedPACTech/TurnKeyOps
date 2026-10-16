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
        "Contacts" or "Customers" or "JobSites" => "contacts",
        "Payroll" => "payroll",
        "Finance" or "FinanceMigration" or "ConstructionFinance" or "FinanceOperations" or "ConstructionPayment" => "finance",
        "Inventory" => "inventory",
        "Purchasing" => "purchasing",
        "Jobs" or "JobWorkspace" or "Locksmith" => "jobs",
        "Calendar" or "Weather" => "calendar",
        "QuoteRequests" or "QuoteRequestAttachments" => "requests",
        "Leads" => "leads",
        "EstimateWorkspace" or "Estimates" or "QuoteEstimates" or "AdminEstimateDefaults" => "estimates",
        "Invoices" or "LocksmithFieldInvoices" => "invoices",
        "Dashboard" => "dashboard",
        "BobIntelligence" => "bob",
        "Bob" or "BobActions" or "Chat" or "AIRealtime" => "bob",
        "PortalAdmin" or "AdminTenantSettings" or "TenantProfile" or "TenantOnboardingPolicy" or "ActivityLogs" => "settings",
        "Billing" or "BillingAdmin" or "TenantBillingAccount" or "TenantSubscription" or "TenantSeatEntitlement" or "TenantCreditBalance" or "TokenLedger" => "billing",
        _ => null
    };
    public static bool AllowsEstimateSubmission(string controller, string action, IEnumerable<string> permissions)
    {
        var set = permissions.ToHashSet();
        if (!set.Contains("estimates.read") || !set.Contains("estimates.write") || !set.Contains("estimates.submit")) return false;
        return controller switch {
            "EstimateWorkspace" => action is "Price" or "Extract" or "Upload" or "Submit",
            "QuoteEstimates" => action == "SaveDraft",
            "Estimates" => action is "Submit" or "Calculate" or "CalculateConcrete" or "UpdateStructured" or "CreateFromAppointment",
            _ => false
        };
    }
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
        if(action.ControllerName=="Payroll"&&action.ActionName=="Bob"&&context.ActionArguments.TryGetValue("input",out var payrollProposal)&&payrollProposal is TurnKeyOps.Lib.Dtos.ProposeBobActionDto payrollAction&&payrollAction.ToolKey=="payroll.readiness")write=false;
        if(action.ControllerName=="FinanceMigration"&&action.ActionName=="Preview")write=false;
        if(action.ControllerName=="Finance"&&action.ActionName=="Bob"&&context.ActionArguments.TryGetValue("input",out var financeProposal)&&financeProposal is TurnKeyOps.Lib.Dtos.ProposeBobActionDto financeAction&&financeAction.ToolKey!="finance.draft-journal")write=false;
        if(action.ControllerName=="JobWorkspace"&&action.ActionName=="Bob"&&context.ActionArguments.TryGetValue("input",out var proposal)&&proposal is TurnKeyOps.Lib.Dtos.ProposeBobActionDto jobAction&&jobAction.ToolKey is "job.summarize" or "job.readiness" or "job.recommend" or "job.structure")write=false;
        if(action.ControllerName is "Inventory" or "Purchasing" && action.ActionName=="Bob" && context.ActionArguments.TryGetValue("input",out var supplyProposal)&&supplyProposal is TurnKeyOps.Lib.Dtos.ProposeBobActionDto supplyAction&&supplyAction.ToolKey is "supply.availability" or "supply.shortages" or "supply.sources" or "supply.purchasing")write=false;
        if (module == "users" && write && action.ControllerName != "People" && !await access.IsOwnerAsync(context.HttpContext.RequestAborted)) {
            context.Result = new ObjectResult(new { error = "Only an owner can change access roles and invitations." }) { StatusCode = 403 };
            return;
        }
        var permissions = await access.GetAsync(context.HttpContext.RequestAborted);
        if (module == "estimates" && write && !permissions.Contains("estimates.manage") && !AllowsEstimateSubmission(action.ControllerName, action.ActionName, permissions)) {
            context.Result = new ObjectResult(new { error = "This role can draft and submit estimates. Managing or issuing proposals requires additional access." }) { StatusCode = 403 };
            return;
        }
        var allowed = action.ControllerName == "BobIntelligence"
            ? permissions.Contains("bob.read") && (!write || permissions.Contains("bob.write") || action.ActionName is "Command" or "Feedback" or "Dismiss")
            : UserModulePermissions.Allows(permissions, module, write);
        if (!allowed) {
            context.Result = new ObjectResult(new { error = "You do not have access to this module." }) { StatusCode = 403 };
            return;
        }
        await next();
    }
}
