using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.API.Middleware
{

    /// <summary>
    /// Middleware to handle API exceptions globally
    /// Catches unhandled exceptions, logs them, and returns standardized error responses
    /// Also saves error details to SystemErrorRepository for tracking
    /// </summary>
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;
        private readonly ApiErrorHandlingOptions _options;
        private readonly ISystemErrorRepository _systemErrorRepository;

        public ApiExceptionMiddleware(RequestDelegate next,
                                      ILogger<ApiExceptionMiddleware> logger,
                                      IOptions<ApiErrorHandlingOptions> options,
                                    ISystemErrorRepository systemErrorRepository)
        {
            _next = next;
            _logger = logger;
            _options = options.Value;
            _systemErrorRepository = systemErrorRepository;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException vex) // <-- custom service validation exception
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";

                var response = new ApiResponse<object>
                {
                    Success = false,
                    Errors = vex.Errors.Select(e => new ApiError
                    {
                        Code = e.Code,
                        Field = e.Field,
                        Message = e.Message
                    }).ToList(),
                    TraceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(response);
            }
            catch (ArgumentException aex)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";

                var response = new ApiResponse<object>
                {
                    Success = false,
                    Errors = new List<ApiError>
                {
                    new ApiError
                    {
                        Code = "Validation",
                        Field = aex.ParamName,
                        Message = _options.ExposeDetailedErrors ? aex.Message : "The request is invalid."
                    }
                },
                    TraceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(response);
            }
            catch (UnauthorizedAccessException uaex)
            {
                _logger.LogWarning(uaex, "Unauthorized request");

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new ApiResponse<object>
                {
                    Success = false,
                    Errors = new List<ApiError>
                {
                    new ApiError
                    {
                        Code = "Unauthorized",
                        Message = _options.ExposeDetailedErrors ? uaex.Message : "Unauthorized"
                    }
                },
                    TraceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(response);
            }
            catch (ForbiddenAccessException faex)
            {
                _logger.LogWarning(faex, "Forbidden request");

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var response = new ApiResponse<object>
                {
                    Success = false,
                    Errors = new List<ApiError>
                {
                    new ApiError
                    {
                        Code = "Forbidden",
                        Message = _options.ExposeDetailedErrors ? faex.Message : "Forbidden"
                    }
                },
                    TraceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(response);
            }
            catch (TooManyRequestsException tmrex)
            {
                _logger.LogWarning(tmrex, "Rate-limited request");

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";

                var response = new ApiResponse<object>
                {
                    Success = false,
                    Errors = new List<ApiError>
                {
                    new ApiError
                    {
                        Code = "TooManyRequests",
                        Message = _options.ExposeDetailedErrors ? tmrex.Message : "Too many requests"
                    }
                },
                    TraceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                //TODO: handle logger configuration (e.g., Sentry, Application Insights)
                _logger.LogError(ex, "Unhandled exception");

                // Save error to Azure Table via repository
                await _systemErrorRepository.SaveAsync(new SystemError
                {
                    PartitionKey = DateTime.UtcNow.ToString("yyyyMMdd"),
                    RowKey = Guid.NewGuid().ToString(),
                    Path = context.Request.Path,
                    Method = context.Request.Method,
                    Message = ex.Message,
                    StackTrace = ex.ToString(),
                    TraceId = context.TraceIdentifier,
                    Timestamp = DateTimeOffset.UtcNow
                });

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var response = new ApiResponse<object>
                {
                    Success = false,
                    Errors = new List<ApiError>
                {
                    new ApiError
                    {
                        Code = "UnexpectedError",
                        Message = _options.ExposeDetailedErrors ? ex.Message : "An unexpected error occurred"
                    }
                },
                    TraceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(response);
            }
        }
    }
}
