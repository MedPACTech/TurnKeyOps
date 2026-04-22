namespace MedInsights.Services.Interfaces
{
    public interface IStartupSeeder
    {
        Task SeedAsync(CancellationToken ct = default);
    }
}
