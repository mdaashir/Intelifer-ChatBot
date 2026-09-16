using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Domain.Entities;
using Intellifer.Chatbot.Domain.Enums;
using Intellifer.Chatbot.Infrastructure.Persistence;
using Intellifer.Chatbot.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Intellifer.Chatbot.IntegrationTests;

[Collection(ChatbotCollection.Name)]
public sealed class ChatRepositoryTests
{
    private readonly ChatbotEnvironmentFixture _environment;

    public ChatRepositoryTests(ChatbotEnvironmentFixture environment)
    {
        _environment = environment;
    }

    [Fact]
    public async Task AddAsync_RoundTripsAMessageThroughSqlServer()
    {
        await using var context = _environment.CreateContext();
        var userId = await CreateUserAsync();
        var sessionId = Guid.NewGuid();

        var repository = new ChatRepository(context);

        await repository.AddAsync(new ChatMessage
        {
            SessionId = sessionId,
            UserId = userId,
            Sender = MessageSender.User,
            Content = "Hello from the integration test",
            CreatedAtUtc = DateTime.UtcNow
        });

        var stored = await context.ChatMessages
            .AsNoTracking()
            .SingleAsync(message => message.SessionId == sessionId);

        Assert.True(stored.Id > 0);
        Assert.Equal("Hello from the integration test", stored.Content);
        Assert.Equal(MessageSender.User, stored.Sender);
        Assert.Equal(userId, stored.UserId);
    }

    [Fact]
    public async Task GetBySessionAsync_ReturnsMessagesInIdOrder()
    {
        await using var context = _environment.CreateContext();
        var userId = await CreateUserAsync();
        var sessionId = Guid.NewGuid();

        var repository = new ChatRepository(context);

        foreach (var content in new[] { "first", "second", "third" })
        {
            await repository.AddAsync(new ChatMessage
            {
                SessionId = sessionId,
                UserId = userId,
                Sender = MessageSender.User,
                Content = content,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        var messages = await repository.GetBySessionAsync(sessionId, userId);

        Assert.Equal(new[] { "first", "second", "third" }, messages.Select(message => message.Content));
    }

    [Fact]
    public async Task GetBySessionAsync_IgnoresMessagesBelongingToAnotherSession()
    {
        await using var context = _environment.CreateContext();
        var userId = await CreateUserAsync();
        var sessionId = Guid.NewGuid();

        var repository = new ChatRepository(context);

        await repository.AddAsync(CreateMessage(sessionId, userId, "mine"));
        await repository.AddAsync(CreateMessage(Guid.NewGuid(), userId, "another session"));

        var messages = await repository.GetBySessionAsync(sessionId, userId);

        Assert.Equal("mine", Assert.Single(messages).Content);
    }

    [Fact]
    public async Task GetBySessionAsync_IgnoresMessagesBelongingToAnotherUserInTheSameSession()
    {
        await using var context = _environment.CreateContext();
        var ownerId = await CreateUserAsync();
        var intruderId = await CreateUserAsync();
        var sessionId = Guid.NewGuid();

        var repository = new ChatRepository(context);

        await repository.AddAsync(CreateMessage(sessionId, ownerId, "private"));

        var messages = await repository.GetBySessionAsync(sessionId, intruderId);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task DeletingAUser_CascadesToTheirMessages()
    {
        await using var context = _environment.CreateContext();
        var userId = await CreateUserAsync();
        var sessionId = Guid.NewGuid();

        var repository = new ChatRepository(context);

        await repository.AddAsync(CreateMessage(sessionId, userId, "will be removed with the account"));

        var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
        context.Users.Remove(user);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.False(await context.ChatMessages.AnyAsync(message => message.SessionId == sessionId));
    }

    [Fact]
    public async Task ContentLongerThanTheSchemaAllows_IsRejectedBySqlServer()
    {
        await using var context = _environment.CreateContext();
        var userId = await CreateUserAsync();

        var tooLong = new string('a', ChatMessage.MaxContentLength + 1);

        context.ChatMessages.Add(CreateMessage(Guid.NewGuid(), userId, tooLong));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static ChatMessage CreateMessage(Guid sessionId, string userId, string content) => new()
    {
        SessionId = sessionId,
        UserId = userId,
        Sender = MessageSender.User,
        Content = content,
        CreatedAtUtc = DateTime.UtcNow
    };

    private async Task<string> CreateUserAsync() =>
        await _environment.Api.CreateUserAsync(
            $"user-{Guid.NewGuid():N}@example.com",
            "Password123",
            RoleNames.User);
}
