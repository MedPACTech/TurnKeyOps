
using MedInsights.Lib;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {

        protected ApiControllerBase()
        {

        }

        protected IActionResult OkResponse<T>(T data)
        {
            var response = new ApiResponse<T>
            {
                Data = data,
                Success = true,
                TraceId = HttpContext.TraceIdentifier
            };

            return Ok(response);
        }

        protected IActionResult DeletedResponse<T>(T data)
        {
            var response = new ApiResponse<T>
            {
                Data = data,
                Success = true,
                TraceId = HttpContext.TraceIdentifier
            };

            return Ok(response);
        }

        protected IActionResult CreatedResponse<T>(string actionName, object routeValues, T data)
        {
            var response = new ApiResponse<T>
            {
                Data = data,
                Success = true,
                TraceId = HttpContext.TraceIdentifier
            };

            return CreatedAtAction(actionName, routeValues, response);
        }

        protected IActionResult NoContentResponse()
        {
            return NoContent();
        }

        // 🔹 Success paged response
        protected IActionResult OkPagedResponse<T>(
            IEnumerable<T> data,
            int pageSize,
            string? continuationToken)
        {
            var response = new ApiPagedResponse<T>
            {
                Data = data,
                PageSize = pageSize,
                ContinuationToken = continuationToken,
                Success = true,
                TraceId = HttpContext.TraceIdentifier
            };

            return Ok(response);
        }

        // 🔹 Validation or expected error response
        protected IActionResult BadRequestResponse(IEnumerable<ApiError> errors)
        {
            var response = new ApiResponse<object>
            {
                Success = false,
                Errors = errors.ToList(),
                TraceId = HttpContext.TraceIdentifier
            };

            return BadRequest(response);
        }

        protected IActionResult BadRequestResponse(string message, string? field = null, string code = "Validation")
        {
            var errors = new List<ApiError>
            {
                new ApiError { Code = code, Field = field, Message = message }
            };

            return BadRequestResponse(errors);
        }
    }
}
