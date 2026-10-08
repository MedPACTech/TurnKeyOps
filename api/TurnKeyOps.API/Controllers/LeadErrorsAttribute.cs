using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TurnKeyOps.API.Controllers;

// Keep expected workflow failures actionable without exposing infrastructure exceptions.
public sealed class LeadErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var error = context.Exception switch
        {
            ArgumentException argument => (400, argument.Message),
            KeyNotFoundException => (404, "Lead or linked record not found."),
            InvalidOperationException => (409, "The operation could not be completed. Refresh the Lead and review its activity before retrying."),
            _ => (0, "")
        };
        if (error.Item1 == 0) return;
        context.Result = new ObjectResult(new { message = error.Item2 }) { StatusCode = error.Item1 };
        context.ExceptionHandled = true;
    }
}
