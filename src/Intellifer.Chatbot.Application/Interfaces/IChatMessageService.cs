using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;

namespace Intellifer.Chatbot.Application.Interfaces;

public interface IChatMessageService
{
    Task<OperationResult<SendMessageResult>> SaveAndRespondAsync(
        string userId,
        Guid sessionId,
        string? content,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessageDto>> GetHistoryAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
