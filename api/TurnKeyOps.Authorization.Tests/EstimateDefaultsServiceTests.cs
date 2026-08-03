using MedInsights.Lib.Utils;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;

namespace MedInsights.Authorization.Tests;

public sealed class EstimateDefaultsServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task UpsertRejectsNegativeDefaultsBeforePersistence()
    {
        var repository = new Mock<IEstimateDefaultsRepository>();
        var service = new EstimateDefaultsService(repository.Object, new TestTurnKeyUserContext());
        var defaults = await service.GetAsync();
        defaults.ConcreteCostPerYard = -1m;

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.UpsertAsync(defaults));

        Assert.Equal(nameof(EstimateDefaultsDto.ConcreteCostPerYard), exception.ParamName);
        repository.Verify(
            x => x.SaveAsync(It.IsAny<EstimateDefaultsProfile>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpsertRejectsEmptyCrewBeforePersistence()
    {
        var repository = new Mock<IEstimateDefaultsRepository>();
        var service = new EstimateDefaultsService(repository.Object, new TestTurnKeyUserContext());
        var defaults = await service.GetAsync();
        defaults.DefaultCrewSize = 0;

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.UpsertAsync(defaults));

        Assert.Equal(nameof(EstimateDefaultsDto.DefaultCrewSize), exception.ParamName);
        repository.Verify(
            x => x.SaveAsync(It.IsAny<EstimateDefaultsProfile>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpsertPersistsDefaultsInTheCurrentTenantPartition()
    {
        var repository = new Mock<IEstimateDefaultsRepository>();
        repository
            .Setup(x => x.SaveAsync(It.IsAny<EstimateDefaultsProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstimateDefaultsProfile entity, CancellationToken _) => entity);

        var service = new EstimateDefaultsService(repository.Object, new TestTurnKeyUserContext());
        var defaults = await service.GetAsync();
        defaults.ConcreteCostPerYard = 212.50m;

        var saved = await service.UpsertAsync(defaults);

        Assert.Equal(212.50m, saved.ConcreteCostPerYard);
        repository.Verify(
            x => x.SaveAsync(
                It.Is<EstimateDefaultsProfile>(entity =>
                    entity.PartitionKey == TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(TenantId) &&
                    entity.RowKey == "ESTIMATE-DEFAULTS" &&
                    entity.ConcreteCostPerYard == 212.50m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class TestTurnKeyUserContext : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => EstimateDefaultsServiceTests.TenantId;
        public Guid UserId => Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }
}
