using Azure;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Services.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;

namespace MedInsights.Authorization.Tests;

public sealed class TenantSettingsServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task MissingBillingSettingsReturnTypedDefaults()
    {
        var fixture = new Fixture();

        var result = await fixture.Service.GetProtectedAsync(TenantSettingKinds.Billing);

        Assert.Equal(50m, result.Values.GetProperty("depositPercentRequired").GetDecimal());
        Assert.Equal(1, result.SchemaVersion);
        Assert.False(result.IsPublic);
        fixture.RoleAccess.Verify(
            access => access.RequirePermissionAsync(
                TurnKeyPermissionKeys.TenantSettingsRead,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpsertPersistsOnlyInCurrentTenantPartitionAndSurvivesServiceRecreation()
    {
        TenantSettingsDocument? persisted = null;
        var fixture = new Fixture();
        fixture.Repository
            .Setup(repository => repository.SaveAsync(
                It.IsAny<TenantSettingsDocument>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSettingsDocument entity, CancellationToken _) =>
            {
                entity.ETag = new ETag("v1");
                persisted = entity;
                return entity;
            });

        var saved = await fixture.Service.UpsertAsync(
            TenantSettingKinds.Billing,
            Input(new { depositPercentRequired = 35m }));

        Assert.NotNull(persisted);
        Assert.Equal(
            TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(TenantId),
            persisted!.PartitionKey);
        Assert.DoesNotContain(OtherTenantId.ToString("N"), persisted.PartitionKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("v1", saved.Version);

        var restarted = new Fixture();
        restarted.Repository
            .Setup(repository => repository.GetAsync(
                persisted.PartitionKey,
                persisted.RowKey,
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(persisted);

        var readBack = await restarted.Service.GetProtectedAsync(TenantSettingKinds.Billing);
        Assert.Equal(35m, readBack.Values.GetProperty("depositPercentRequired").GetDecimal());
    }

    [Fact]
    public async Task UpsertRejectsStaleVersionBeforeSaving()
    {
        var fixture = new Fixture();
        fixture.Repository
            .Setup(repository => repository.GetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                true))
            .ReturnsAsync(Entity(TenantSettingKinds.Billing, "v2", "{\"depositPercentRequired\":50}"));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpsertAsync(
            TenantSettingKinds.Billing,
            Input(new { depositPercentRequired = 40m }, expectedVersion: "v1")));

        Assert.Contains("changed after", exception.Message, StringComparison.OrdinalIgnoreCase);
        fixture.Repository.Verify(
            repository => repository.SaveAsync(
                It.IsAny<TenantSettingsDocument>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublicContentCannotPersistSecretsOrSensitivePayloadKeys()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpsertAsync(
            TenantSettingKinds.PublicContent,
            Input(new { hero = new { apiKey = "raw-value" } })));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpsertAsync(
            TenantSettingKinds.PublicContent,
            Input(
                new { hero = new { title = "Safe" } },
                secretReferences: new() { ["maps"] = "keyvault://tenant/maps" })));
    }

    [Fact]
    public async Task PublicContentRejectsIncompleteDocument()
    {
        var fixture = new Fixture();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpsertAsync(
            TenantSettingKinds.PublicContent,
            Input(new { hero = new { title = "Incomplete" } })));

        Assert.Contains("navigation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProtectedReadReturnsOnlyConfiguredSecretNames()
    {
        var fixture = new Fixture();
        var entity = Entity(TenantSettingKinds.Brand, "v4", "{\"accent\":\"#f97316\"}");
        entity.SecretReferencesJson = "{\"mapsApi\":\"keyvault://tenant/maps-api\"}";
        fixture.Repository
            .Setup(repository => repository.GetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(entity);

        var result = await fixture.Service.GetProtectedAsync(TenantSettingKinds.Brand);
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.Equal(["mapsApi"], result.ConfiguredSecretKeys);
        Assert.DoesNotContain("keyvault://", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenant/maps-api", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OperationalValidationRejectsInvalidPercentCrewAndLists()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpsertAsync(
            TenantSettingKinds.Operational,
            Input(new { defaultCrewSize = 0, depositPercentRequired = 25, services = new[] { "Clearing" } })));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpsertAsync(
            TenantSettingKinds.Operational,
            Input(new { defaultCrewSize = 3, depositPercentRequired = 101, services = new[] { "Clearing" } })));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpsertAsync(
            TenantSettingKinds.Operational,
            Input(new { defaultCrewSize = 3, depositPercentRequired = 25, services = new[] { "" } })));
    }

    private static UpdateTenantSettingsDocumentDto Input(
        object values,
        string? expectedVersion = null,
        Dictionary<string, string>? secretReferences = null) => new()
    {
        Values = System.Text.Json.JsonSerializer.SerializeToElement(values),
        ExpectedVersion = expectedVersion,
        SecretReferences = secretReferences ?? new(StringComparer.OrdinalIgnoreCase)
    };

    private static TenantSettingsDocument Entity(string kind, string version, string valuesJson) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        PartitionKey = TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(TenantId),
        RowKey = $"SETTINGS|{kind.ToUpperInvariant()}",
        Kind = kind,
        SchemaVersion = 1,
        IsPublic = kind == TenantSettingKinds.PublicContent,
        ValuesJson = valuesJson,
        SecretReferencesJson = "{}",
        ETag = new ETag(version),
        DateCreated = DateTime.UtcNow,
        DateUpdated = DateTime.UtcNow
    };

    private sealed class Fixture
    {
        public Mock<ITenantSettingsRepository> Repository { get; } = new();
        public Mock<IRoleAccessService> RoleAccess { get; } = new();
        public Mock<IAuditService> Audit { get; } = new();
        public TenantSettingsService Service { get; }

        public Fixture()
        {
            RoleAccess
                .Setup(access => access.RequirePermissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Audit
                .Setup(audit => audit.RecordAsync(It.IsAny<RecordAuditEventRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuditEventDto());
            Service = new TenantSettingsService(
                Repository.Object,
                new TestTurnKeyUserContext(),
                RoleAccess.Object,
                Audit.Object);
        }
    }

    private sealed class TestTurnKeyUserContext : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => TenantSettingsServiceTests.TenantId;
        public Guid UserId => Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }
}
