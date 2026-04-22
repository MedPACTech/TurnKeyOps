using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;

namespace MedInsights.API.Infrastructure
{
    /// <summary>
    /// Handles non-request pipeline errors:
    /// - Startup exceptions
    /// - Background worker crashes
    /// - Unobserved Task exceptions
    /// </summary>
    public class GlobalErrorHandler : IHostedService
    {
        private readonly ILogger<GlobalErrorHandler> _logger;
        private readonly ISystemErrorRepository _systemErrorRepository;

        public GlobalErrorHandler(ILogger<GlobalErrorHandler> logger,
                                  ISystemErrorRepository systemErrorRepository)
        {
            _logger = logger;
            _systemErrorRepository = systemErrorRepository;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            AppDomain.CurrentDomain.UnhandledException += async (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    await HandleExceptionAsync(ex, "AppDomain.UnhandledException");
                }
            };

            TaskScheduler.UnobservedTaskException += async (sender, e) =>
            {
                await HandleExceptionAsync(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved(); // avoid process crash
            };

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Explicitly capture startup exceptions before middleware runs
        /// </summary>
        public async Task HandleStartupExceptionAsync(Exception ex)
        {
            await HandleExceptionAsync(ex, "StartupException");
        }

        private async Task HandleExceptionAsync(Exception ex, string source)
        {
            try
            {
                try
                {
                    _logger.LogCritical(ex, "Global error from {Source}", source);
                }
                catch (Exception loggerEx)
                {
                    Console.Error.WriteLine($"Global error from {source}: {ex}");
                    Console.Error.WriteLine($"Logger failure while recording global error: {loggerEx}");
                }

                await _systemErrorRepository.SaveAsync(new SystemError
                {
                    PartitionKey = DateTime.UtcNow.ToString("yyyyMMdd"),
                    RowKey = Guid.NewGuid().ToString(),
                    Path = source,
                    Method = "N/A",
                    Message = ex.Message,
                    StackTrace = ex.ToString(),
                    TraceId = Guid.NewGuid().ToString(),
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception logEx)
            {
                Console.Error.WriteLine($"Failed to persist global error from {source}: {logEx}");
            }
        }
    }
}
