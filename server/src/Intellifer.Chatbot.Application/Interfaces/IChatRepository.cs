using Intellifer.Chatbot.Domain.Entities;

namespace Intellifer.Chatbot.Application.Interfaces;

public interface IChatRepository
{
    Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
        Guid sessionId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent <paramref name="count"/> messages of a session, returned
    /// oldest first so they read in conversation order.
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> GetRecentAsync(
        Guid sessionId,
        string userId,
        int count,
        CancellationToken cancellationToken = default);
}
