namespace MedInsights.Services.Interfaces
{
    public interface IStartupSeedContributor
    {
        Task SeedAsync(CancellationToken ct = default);
    }
}
