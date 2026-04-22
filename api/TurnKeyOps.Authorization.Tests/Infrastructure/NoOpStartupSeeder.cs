using MedInsights.Services.Interfaces;

namespace MedInsights.Authorization.Tests.Infrastructure;

internal sealed class NoOpStartupSeeder : IStartupSeeder
{
    public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
}
