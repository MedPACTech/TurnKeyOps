using System.Text.Json;
using MedInsights.Lib;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;
using Moq;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;

namespace MedInsights.Authorization.Tests;

public sealed class BobOperationsServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ConversationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task SafeReadExecutesImmediatelyAndRedactsStoredSecrets()
    {
        var provider = new TestProvider("conversation.read", BobActionRisk.Read, TurnKeyPermissionKeys.OperationsRead);
        var fixture = new Fixture(provider);

        var result = await fixture.Service.ProposeAsync(
            ConversationId,
            Proposal("read-1", new
            {
                filter = "summary",
                authorization = "Bearer secret",
                customerEmail = "customer@example.com",
                serviceAddress = "123 Main Street"
            }));

        Assert.Equal("completed", result.Status);
        Assert.False(result.ConfirmationRequired);
        Assert.Equal(1, provider.ExecutionCount);
        var stored = Assert.Single(fixture.Store.Values);
        Assert.Contains("[REDACTED]", stored.InputJson);
        Assert.DoesNotContain("Bearer secret", stored.InputJson);
        Assert.DoesNotContain("customer@example.com", stored.InputJson);
        Assert.DoesNotContain("123 Main Street", stored.InputJson);
        fixture.RoleAccess.Verify(
            service => service.RequirePermissionAsync(TurnKeyPermissionKeys.OperationsRead, It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Audit.Verify(
            service => service.RecordAsync(
                It.Is<RecordAuditEventRequestDto>(audit => audit.Category == "bob_action"),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SensitiveWriteRequiresApprovalAndReplayDoesNotExecuteTwice()
    {
        var provider = new TestProvider("conversation.archive", BobActionRisk.Destructive, TurnKeyPermissionKeys.OperationsManage);
        var fixture = new Fixture(provider);
        var proposal = Proposal("archive-1", new { reason = "operator request" });

        var proposed = await fixture.Service.ProposeAsync(ConversationId, proposal);
        var replay = await fixture.Service.ProposeAsync(ConversationId, proposal);

        Assert.Equal(proposed.Id, replay.Id);
        Assert.Equal("proposed", proposed.Status);
        Assert.True(proposed.ConfirmationRequired);
        Assert.Equal(0, provider.ExecutionCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ExecuteAsync(proposed.Id));

        var approved = await fixture.Service.ApproveAsync(proposed.Id);
        var executed = await fixture.Service.ExecuteAsync(approved.Id);
        var completedReplay = await fixture.Service.ExecuteAsync(approved.Id);

        Assert.Equal("approved", approved.Status);
        Assert.Equal("completed", executed.Status);
        Assert.Equal(executed.Id, completedReplay.Id);
        Assert.Equal(1, provider.ExecutionCount);
    }

    [Fact]
    public async Task PermissionDenialPreventsProposalPersistence()
    {
        var provider = new TestProvider("conversation.archive", BobActionRisk.Destructive, TurnKeyPermissionKeys.OperationsManage);
        var fixture = new Fixture(provider);
        fixture.RoleAccess
            .Setup(service => service.RequirePermissionAsync(
                TurnKeyPermissionKeys.OperationsManage,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException("denied"));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            fixture.Service.ProposeAsync(ConversationId, Proposal("denied-1", new { })));

        Assert.Empty(fixture.Store);
        Assert.Equal(0, provider.ExecutionCount);
    }

    [Fact]
    public async Task ProviderFailureIsDurableAuditedAndRetryable()
    {
        var provider = new TestProvider("conversation.archive", BobActionRisk.Destructive, TurnKeyPermissionKeys.OperationsManage)
        {
            Fail = true
        };
        var fixture = new Fixture(provider);
        var proposed = await fixture.Service.ProposeAsync(
            ConversationId,
            Proposal("retry-1", new { reason = "cleanup" }));
        await fixture.Service.ApproveAsync(proposed.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ExecuteAsync(proposed.Id));
        Assert.Equal("failed", fixture.Store[proposed.Id].Status);
        Assert.Equal(nameof(InvalidOperationException), fixture.Store[proposed.Id].FailureCode);

        provider.Fail = false;
        var retried = await fixture.Service.ExecuteAsync(proposed.Id);

        Assert.Equal("completed", retried.Status);
        Assert.Equal(2, provider.ExecutionCount);
        fixture.Audit.Verify(
            service => service.RecordAsync(
                It.Is<RecordAuditEventRequestDto>(audit => audit.Action == "failed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConversationLookupUsesCurrentTenantAndActorPartition()
    {
        var provider = new TestProvider("conversation.read", BobActionRisk.Read, TurnKeyPermissionKeys.OperationsRead);
        var fixture = new Fixture(provider, tenantId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        fixture.ChatRepository
            .Setup(repository => repository.GetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync((Chat?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Service.ProposeAsync(ConversationId, Proposal("tenant-1", new { filter = "summary" })));

        fixture.ChatRepository.Verify(repository => repository.GetAsync(
            EntityKeyPolicy.TenantUserPartition(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), ActorId),
            EntityKeyPolicy.Row(ConversationId),
            It.IsAny<CancellationToken>(),
            false));
        Assert.Empty(fixture.Store);
    }

    [Fact]
    public async Task WriteKillSwitchFailsClosedButReadRemainsAvailable()
    {
        var writeProvider = new TestProvider("conversation.archive", BobActionRisk.Destructive, TurnKeyPermissionKeys.OperationsManage);
        var fixture = new Fixture(writeProvider, writeActionsEnabled: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ProposeAsync(ConversationId, Proposal("disabled-1", new { })));

        Assert.Empty(fixture.Store);
        Assert.True(BobOperationsService.RequiresConfirmation(BobActionRisk.Financial));
        Assert.True(BobOperationsService.RequiresConfirmation(BobActionRisk.Scheduling));
        Assert.True(BobOperationsService.RequiresConfirmation(BobActionRisk.CustomerFacing));
        Assert.False(BobOperationsService.RequiresConfirmation(BobActionRisk.Read));
    }

    private static ProposeBobActionDto Proposal(string idempotencyKey, object input) => new()
    {
        ToolKey = input.GetType().GetProperty("filter") is not null ? "conversation.read" : "conversation.archive",
        IdempotencyKey = idempotencyKey,
        Input = JsonSerializer.SerializeToElement(input)
    };

    private sealed class Fixture
    {
        public Dictionary<Guid, BobActionRecord> Store { get; } = new();
        public Mock<IBobActionRepository> Repository { get; } = new();
        public Mock<MedInsights.Repositories.Interfaces.IChatRepository> ChatRepository { get; } = new();
        public Mock<IRoleAccessService> RoleAccess { get; } = new();
        public Mock<IAuditService> Audit { get; } = new();
        public BobOperationsService Service { get; }

        public Fixture(
            IBobActionProvider provider,
            Guid? tenantId = null,
            bool writeActionsEnabled = true)
        {
            Repository
                .Setup(repository => repository.SaveAsync(It.IsAny<BobActionRecord>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BobActionRecord entity, CancellationToken _) =>
                {
                    Store[entity.Id] = entity;
                    return entity;
                });
            Repository
                .Setup(repository => repository.GetAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string partition, Guid id, CancellationToken _) =>
                    Store.TryGetValue(id, out var entity) && entity.PartitionKey == partition ? entity : null);
            Repository
                .Setup(repository => repository.FindByIdempotencyKeyAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string partition, string key, CancellationToken _) =>
                    Store.Values.FirstOrDefault(entity => entity.PartitionKey == partition && entity.IdempotencyKey == key));
            Repository
                .Setup(repository => repository.ListByConversationAsync(
                    It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string partition, Guid conversation, CancellationToken _) =>
                    (IReadOnlyList<BobActionRecord>)Store.Values
                        .Where(entity => entity.PartitionKey == partition && entity.ConversationId == conversation)
                        .ToList());

            ChatRepository
                .Setup(repository => repository.GetAsync(
                    It.IsAny<string>(),
                    EntityKeyPolicy.Row(ConversationId),
                    It.IsAny<CancellationToken>(),
                    false))
                .ReturnsAsync(new Chat { Id = ConversationId });
            RoleAccess
                .Setup(service => service.RequirePermissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Audit
                .Setup(service => service.RecordAsync(
                    It.IsAny<RecordAuditEventRequestDto>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuditEventDto());

            Service = new BobOperationsService(
                Repository.Object,
                ChatRepository.Object,
                [provider],
                new TestUserContext(tenantId ?? TenantId),
                RoleAccess.Object,
                Audit.Object,
                new BobContextMinimizer(),
                Options.Create(new BobOperationsOptions
                {
                    Enabled = true,
                    WriteActionsEnabled = writeActionsEnabled,
                    MaxStoredInputCharacters = 2_000
                }));
        }
    }

    private sealed class TestProvider : IBobActionProvider
    {
        public TestProvider(string toolKey, BobActionRisk risk, string permissionKey)
        {
            ToolKey = toolKey;
            Risk = risk;
            PermissionKey = permissionKey;
        }

        public string ToolKey { get; }
        public string PermissionKey { get; }
        public BobActionRisk Risk { get; }
        public bool Fail { get; set; }
        public int ExecutionCount { get; private set; }

        public Task<object?> ExecuteAsync(
            BobActionExecutionContext context,
            JsonElement input,
            CancellationToken ct = default)
        {
            ExecutionCount++;
            if (Fail) throw new InvalidOperationException("provider unavailable");
            return Task.FromResult<object?>(new { ok = true, context.ConversationId });
        }
    }

    private sealed class TestUserContext : TurnKeyOps.Lib.Utils.IUserContext
    {
        public TestUserContext(Guid tenantId) => TenantId = tenantId;
        public bool IsAuthenticated => true;
        public Guid TenantId { get; }
        public Guid UserId => ActorId;
        public MedInsights.Lib.Utils.AppTimeZone Timezone => MedInsights.Lib.Utils.AppTimeZone.Utc;
        public string FirstName => "Bob";
        public string LastName => "Tester";
    }
}
