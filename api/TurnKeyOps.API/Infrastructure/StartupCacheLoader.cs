using MedInsights.Lib.Configurations;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.API.Infrastructure
{
    public class StartupCacheLoader : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly DiagnosisCodeCacheSettings _settings;
        private readonly ILogger<StartupCacheLoader> _logger;

        public StartupCacheLoader(
            IServiceProvider serviceProvider,
            IHostEnvironment hostEnvironment,
            IOptions<DiagnosisCodeCacheSettings> settings,
            ILogger<StartupCacheLoader> logger)
        {
            _serviceProvider = serviceProvider;
            _hostEnvironment = hostEnvironment;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_settings.WarmOnStartup)
            {
                _logger.LogInformation("Diagnosis code startup cache warmup disabled.");
                return;
            }

            if (_hostEnvironment.IsDevelopment() && _settings.SkipWarmupInDevelopment)
            {
                _logger.LogInformation("Skipping diagnosis code startup cache warmup in Development.");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var diagnosisCodeService = scope.ServiceProvider.GetRequiredService<IDiagnosisCodeService>();
            await diagnosisCodeService.WarmCacheAsync(cancellationToken);

            _logger.LogInformation("Diagnosis code cache warmup completed.");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
