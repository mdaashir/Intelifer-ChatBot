using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Services;
using Intellifer.Chatbot.Domain.Entities;
using Intellifer.Chatbot.Domain.Enums;
using Intellifer.Chatbot.UnitTests.Fakes;

namespace Intellifer.Chatbot.UnitTests.Services;

public sealed class ChatMessageServiceTests
{
    private const string UserId = "user-1";

    private static readonly Guid SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeChatRepository _repository = new();
    private readonly FakeBotResponder _botResponder = new();
    private readonly FakeClock _clock = new(Now);

    [Fact]
    public async Task SaveAndRespondAsync_PersistsTheUserMessageThenTheBotReply()
    {
        var result = await CreateService().SaveAndRespondAsync(UserId, SessionId, "hello");

        Assert.True(result.Succeeded);
        Assert.Equal(2, _repository.Messages.Count);

        Assert.Equal(MessageSender.User, _repository.Messages[0].Sender);
        Assert.Equal("hello", _repository.Messages[0].Content);

        Assert.Equal(MessageSender.Bot, _repository.Messages[1].Sender);
        Assert.Equal("bot reply", _repository.Messages[1].Content);
    }

    [Fact]
    public async Task SaveAndRespondAsync_GivesTheReplyAHigherIdSoItAlwaysSortsAfterTheMessage()
    {
        var result = await CreateService().SaveAndRespondAsync(UserId, SessionId, "hello");

        Assert.True(result.Value!.BotReply.Id > result.Value.UserMessage.Id);
    }

    [Fact]
    public async Task SaveAndRespondAsync_StoresBothMessagesAgainstTheSessionAndTheUser()
    {
        await CreateService().SaveAndRespondAsync(UserId, SessionId, "hello");

        Assert.All(_repository.Messages, message =>
        {
            Assert.Equal(SessionId, message.SessionId);
            Assert.Equal(UserId, message.UserId);
        });
    }

    [Fact]
    public async Task SaveAndRespondAsync_TrimsTheContentBeforePersistingAndBeforeReplying()
    {
        await CreateService().SaveAndRespondAsync(UserId, SessionId, "   hello   ");

        Assert.Equal("hello", _repository.Messages[0].Content);
        Assert.Equal("hello", Assert.Single(_botResponder.ReceivedConversations).Prompt);
    }

    [Fact]
    public async Task SaveAndRespondAsync_TimestampsBothMessagesFromTheClock()
    {
        await CreateService().SaveAndRespondAsync(UserId, SessionId, "hello");

        Assert.All(_repository.Messages, message => Assert.Equal(Now, message.CreatedAtUtc));
    }

    [Fact]
    public async Task SaveAndRespondAsync_OnAFreshSession_GivesTheResponderNoHistory()
    {
        await CreateService().SaveAndRespondAsync(UserId, SessionId, "hello");

        Assert.Empty(Assert.Single(_botResponder.ReceivedConversations).History);
    }

    [Fact]
    public async Task SaveAndRespondAsync_GivesTheResponderTheEarlierTurnsButNotThePrompt()
    {
        var service = CreateService();
        await service.SaveAndRespondAsync(UserId, SessionId, "first");
        await service.SaveAndRespondAsync(UserId, SessionId, "second");

        var conversation = _botResponder.ReceivedConversations[^1];

        Assert.Equal("second", conversation.Prompt);
        Assert.Equal(
            new[] { "first", "bot reply" },
            conversation.History.Select(turn => turn.Content));
        Assert.Equal(
            new[] { MessageSender.User, MessageSender.Bot },
            conversation.History.Select(turn => turn.Sender));
    }

    [Fact]
    public async Task SaveAndRespondAsync_DoesNotLeakAnotherSessionsTurnsIntoTheHistory()
    {
        var service = CreateService();
        await service.SaveAndRespondAsync(UserId, Guid.NewGuid(), "elsewhere");
        await service.SaveAndRespondAsync(UserId, SessionId, "here");

        var conversations = _botResponder.ReceivedConversations;

        // Neither session may see the other's turns, in either direction.
        Assert.Equal(2, conversations.Count);
        Assert.All(conversations, conversation => Assert.Empty(conversation.History));
    }

    [Fact]
    public async Task SaveAndRespondAsync_BoundsHowMuchHistoryItLoads()
    {
        var service = CreateService();

        for (var index = 0; index < 25; index++)
        {
            await service.SaveAndRespondAsync(UserId, SessionId, $"message {index}");
        }

        Assert.Equal(
            BotConversation.MaxHistoryTurns,
            _botResponder.ReceivedConversations[^1].History.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAndRespondAsync_WhenContentIsBlank_FailsAndPersistsNothing(string? content)
    {
        var result = await CreateService().SaveAndRespondAsync(UserId, SessionId, content);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(_repository.Messages);
        Assert.Empty(_botResponder.ReceivedConversations);
    }

    [Fact]
    public async Task SaveAndRespondAsync_WhenContentExceedsTheLimit_FailsAndPersistsNothing()
    {
        var tooLong = new string('a', ChatMessage.MaxContentLength + 1);

        var result = await CreateService().SaveAndRespondAsync(UserId, SessionId, tooLong);

        Assert.False(result.Succeeded);
        Assert.Empty(_repository.Messages);
    }

    [Fact]
    public async Task SaveAndRespondAsync_WhenContentIsExactlyTheLimit_Succeeds()
    {
        var atTheLimit = new string('a', ChatMessage.MaxContentLength);

        var result = await CreateService().SaveAndRespondAsync(UserId, SessionId, atTheLimit);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SaveAndRespondAsync_WhenTheSessionIdIsEmpty_FailsAndPersistsNothing()
    {
        var result = await CreateService().SaveAndRespondAsync(UserId, Guid.Empty, "hello");

        Assert.False(result.Succeeded);
        Assert.Empty(_repository.Messages);
    }

    [Fact]
    public async Task SaveAndRespondAsync_WhenTheUserIsMissing_FailsAndPersistsNothing()
    {
        var result = await CreateService().SaveAndRespondAsync("   ", SessionId, "hello");

        Assert.False(result.Succeeded);
        Assert.Empty(_repository.Messages);
    }

    [Fact]
    public async Task SaveAndRespondAsync_KeepsTheUserMessageWhenTheBotFails()
    {
        _botResponder.Throws = new InvalidOperationException("the bot exploded");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().SaveAndRespondAsync(UserId, SessionId, "hello"));

        var persisted = Assert.Single(_repository.Messages);
        Assert.Equal(MessageSender.User, persisted.Sender);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsTheConversationInOrder()
    {
        var service = CreateService();
        await service.SaveAndRespondAsync(UserId, SessionId, "first");
        await service.SaveAndRespondAsync(UserId, SessionId, "second");

        var history = await service.GetHistoryAsync(UserId, SessionId);

        Assert.Equal(
            new[] { "first", "bot reply", "second", "bot reply" },
            history.Select(message => message.Content));
    }

    [Fact]
    public async Task GetHistoryAsync_DoesNotReturnMessagesFromAnotherSession()
    {
        var service = CreateService();
        await service.SaveAndRespondAsync(UserId, SessionId, "mine");
        await service.SaveAndRespondAsync(UserId, Guid.NewGuid(), "a different session");

        var history = await service.GetHistoryAsync(UserId, SessionId);

        Assert.Equal(2, history.Count);
        Assert.All(history, message => Assert.Equal(SessionId, message.SessionId));
    }

    [Fact]
    public async Task GetHistoryAsync_DoesNotReturnAnotherUsersMessagesForTheSameSession()
    {
        var service = CreateService();
        await service.SaveAndRespondAsync("user-1", SessionId, "mine");

        var history = await service.GetHistoryAsync("user-2", SessionId);

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetHistoryAsync_WithAnEmptySessionId_ReturnsNothing()
    {
        var service = CreateService();
        await service.SaveAndRespondAsync(UserId, SessionId, "mine");

        Assert.Empty(await service.GetHistoryAsync(UserId, Guid.Empty));
    }

    private ChatMessageService CreateService() => new(_repository, _botResponder, _clock);
}
