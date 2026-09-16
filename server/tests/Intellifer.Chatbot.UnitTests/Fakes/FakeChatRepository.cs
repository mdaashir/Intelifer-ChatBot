using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Domain.Entities;

namespace Intellifer.Chatbot.UnitTests.Fakes;

public sealed class FakeChatRepository : IChatRepository
{
    private readonly List<ChatMessage> _messages = [];

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        message.Id = _messages.Count == 0 ? 1 : _messages.Max(stored => stored.Id) + 1;

        _messages.Add(message);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
        Guid sessionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ChatMessage> matching = _messages
            .Where(message => message.SessionId == sessionId && message.UserId == userId)
            .OrderBy(message => message.Id)
            .ToList();

        return Task.FromResult(matching);
    }

    public Task<IReadOnlyList<ChatMessage>> GetRecentAsync(
        Guid sessionId,
        string userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ChatMessage> recent = _messages
            .Where(message => message.SessionId == sessionId && message.UserId == userId)
            .OrderBy(message => message.Id)
            .TakeLast(count)
            .ToList();

        return Task.FromResult(recent);
    }
}
