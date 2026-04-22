using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class StartupSeeder : IStartupSeeder
    {
        private readonly IEnumerable<IStartupSeedContributor> _contributors;

        public StartupSeeder(IEnumerable<IStartupSeedContributor> contributors)
        {
            _contributors = contributors;
        }

        public async Task SeedAsync(CancellationToken ct = default)
        {
            foreach (var contributor in _contributors)
                await contributor.SeedAsync(ct);
        }
    }
}
