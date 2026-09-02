using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;
using Moq;
using OpenAI;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;

namespace MedInsights.Authorization.Tests;

public sealed class TurnKeyChatServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ActorId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ConversationId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task ConversationAndMessagesSurviveServiceRecreationAndReplayIsIdempotent()
    {
        var state = new State();
        var first = new Fixture(TenantA, state);

        var created = await first.Service.CreateChatAsync(new CreateTurnKeyChatDto
        {
            Id = ConversationId,
            Title = "Estimate follow-up",
            Mode = "estimate-followup",
            StateJson = "{\"stage\":\"review\"}"
        });
        var initial = await first.Service.AppendMessageAsync(ConversationId, new AppendTurnKeyChatMessageDto
        {
            Role = "assistant",
            Content = "I found two estimates that need attention.",
            MetadataJson = "{\"suggestedReplies\":[\"Show me\"]}",
            IdempotencyKey = "conversation-introduction"
        });
        var replay = await first.Service.AppendMessageAsync(ConversationId, new AppendTurnKeyChatMessageDto
        {
            Role = "assistant",
            Content = "I found two estimates that need attention.",
            MetadataJson = "{\"suggestedReplies\":[\"Show me\"]}",
            IdempotencyKey = "conversation-introduction"
        });

        var restarted = new Fixture(TenantA, state);
        var conversations = (await restarted.Service.GetChatsAsync()).ToList();
        var messages = (await restarted.Service.GetMessagesAsync(ConversationId)).ToList();

        Assert.Equal(ConversationId, created.Id);
        Assert.Equal("estimate-followup", Assert.Single(conversations).Mode);
        Assert.Equal("review", System.Text.Json.JsonDocument.Parse(conversations[0].StateJson).RootElement.GetProperty("stage").GetString());
        Assert.Equal(initial.Id, replay.Id);
        Assert.Single(messages);
        Assert.Equal("conversation-introduction", messages[0].IdempotencyKey);
        Assert.Equal(EntityKeyPolicy.TenantUserPartition(TenantA, ActorId), state.Chats.Values.Single().PartitionKey);
        Assert.Equal(TenantA, state.Messages.Values.Single().TenantId);
        Assert.Equal(ActorId, state.Messages.Values.Single().ActorUserId);
    }

    [Fact]
    public async Task WrongTenantCannotReadOrAppendAnotherTenantsConversation()
    {
        var state = new State();
        var tenantA = new Fixture(TenantA, state);
        await tenantA.Service.CreateChatAsync(new CreateTurnKeyChatDto { Id = ConversationId });

        var tenantB = new Fixture(TenantB, state);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => tenantB.Service.GetMessagesAsync(ConversationId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => tenantB.Service.AppendMessageAsync(
            ConversationId,
            new AppendTurnKeyChatMessageDto
            {
                Role = "user",
                Content = "Cross tenant attempt",
                IdempotencyKey = "cross-tenant"
            }));
        Assert.Empty(state.Messages);
    }

    private sealed class Fixture
    {
        public TurnKeyChatService Service { get; }

        public Fixture(Guid tenantId, State state)
        {
            var chats = new Mock<IChatRepository>();
            chats.Setup(repository => repository.GetAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    false))
                .ReturnsAsync((string partition, string row, CancellationToken _, bool _) =>
                    state.Chats.GetValueOrDefault((partition, row)));
            chats.Setup(repository => repository.GetChatsByUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string partition, CancellationToken _) => state.Chats.Values
                    .Where(chat => chat.PartitionKey == partition && !chat.IsDeleted)
                    .OrderByDescending(chat => chat.DateChatUpdated)
                    .ToList());
            chats.Setup(repository => repository.SaveAsync(
                    It.IsAny<Chat>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Chat chat, CancellationToken _) =>
                {
                    state.Chats[(chat.PartitionKey, chat.RowKey)] = chat;
                    return chat;
                });

            var messages = new Mock<IChatMessageRepository>();
            messages.Setup(repository => repository.GetMessagesByChatAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string partition, Guid chatId, int _, CancellationToken _) => state.Messages.Values
                    .Where(message => message.PartitionKey == partition && message.ChatId == chatId && !message.IsDeleted)
                    .OrderBy(message => message.ChatTimestamp)
                    .ToList());
            messages.Setup(repository => repository.SaveAsync(
                    It.IsAny<ChatMessage>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChatMessage message, CancellationToken _) =>
                {
                    state.Messages[(message.PartitionKey, message.RowKey)] = message;
                    return message;
                });

            var roleAccess = new Mock<IRoleAccessService>();
            roleAccess.Setup(service => service.RequirePermissionAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Service = new TurnKeyChatService(
                chats.Object,
                messages.Object,
                new User(tenantId),
                new OpenAIClient("test-key"),
                Options.Create(new OpenAISettings { Key = "test-key", DefaultModel = "test-model" }),
                roleAccess.Object);
        }
    }

    private sealed class State
    {
        public Dictionary<(string Partition, string Row), Chat> Chats { get; } = [];
        public Dictionary<(string Partition, string Row), ChatMessage> Messages { get; } = [];
    }

    private sealed class User(Guid tenantId) : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => tenantId;
        public Guid UserId => ActorId;
        public MedInsights.Lib.Utils.AppTimeZone Timezone => MedInsights.Lib.Utils.AppTimeZone.Utc;
        public string FirstName => "Bob";
        public string LastName => "Tester";
    }
}
