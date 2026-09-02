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

public sealed class ContactAccessGrantServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task UpsertHashesRowKeyAndPersistsInCurrentTenantPartition()
    {
        ContactAccessGrant? persisted = null;
        var fixture = new Fixture();
        fixture.Repository
            .Setup(repository => repository.SaveAsync(
                It.IsAny<ContactAccessGrant>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactAccessGrant entity, CancellationToken _) =>
            {
                entity.ETag = new ETag("v1");
                persisted = entity;
                return entity;
            });

        var result = await fixture.Service.UpsertAsync(
            "customer-123",
            new UpdateContactAccessGrantDto { Role = "field" });

        Assert.NotNull(persisted);
        Assert.Equal(
            TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(TenantId),
            persisted!.PartitionKey);
        Assert.StartsWith("CONTACT|", persisted.RowKey, StringComparison.Ordinal);
        Assert.DoesNotContain("customer-123", persisted.RowKey, StringComparison.Ordinal);
        Assert.Equal("v1", result.Version);
    }

    [Fact]
    public async Task OwnerGrantRequiresDedicatedOwnerPermission()
    {
        var fixture = new Fixture();
        fixture.RoleAccess
            .Setup(access => access.RequirePermissionAsync(
                TurnKeyPermissionKeys.MembershipOwnerGrant,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MedInsights.Lib.ForbiddenAccessException("denied"));

        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(() => fixture.Service.UpsertAsync(
            "customer-123",
            new UpdateContactAccessGrantDto { Role = "owner" }));

        fixture.Repository.Verify(
            repository => repository.SaveAsync(It.IsAny<ContactAccessGrant>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StaleGrantUpdateIsRejected()
    {
        var fixture = new Fixture();
        fixture.Repository
            .Setup(repository => repository.GetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                true))
            .ReturnsAsync(new ContactAccessGrant
            {
                TenantId = TenantId,
                ContactId = "customer-123",
                ETag = new ETag("v2")
            });

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpsertAsync(
            "customer-123",
            new UpdateContactAccessGrantDto { Role = "office-admin", ExpectedVersion = "v1" }));
    }

    [Fact]
    public async Task InvalidRoleIsRejectedBeforePersistence()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpsertAsync(
            "customer-123",
            new UpdateContactAccessGrantDto { Role = "super-admin" }));

        fixture.Repository.VerifyNoOtherCalls();
    }

    private sealed class Fixture
    {
        public Mock<IContactAccessGrantRepository> Repository { get; } = new();
        public Mock<IRoleAccessService> RoleAccess { get; } = new();
        public Mock<IAuditService> Audit { get; } = new();
        public ContactAccessGrantService Service { get; }

        public Fixture()
        {
            RoleAccess
                .Setup(access => access.RequirePermissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Audit
                .Setup(audit => audit.RecordAsync(It.IsAny<RecordAuditEventRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuditEventDto());
            Service = new ContactAccessGrantService(
                Repository.Object,
                new TestTurnKeyUserContext(),
                RoleAccess.Object,
                Audit.Object);
        }
    }

    private sealed class TestTurnKeyUserContext : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => ContactAccessGrantServiceTests.TenantId;
        public Guid UserId => Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }
}
