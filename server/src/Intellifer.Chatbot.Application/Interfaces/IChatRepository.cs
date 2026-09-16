using Intellifer.Chatbot.Domain.Entities;

namespace Intellifer.Chatbot.Application.Interfaces;

public interface IChatRepository
{
    Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
        Guid sessionId,
        string userId,
        CancellationToken cancellationToken = default);
}
