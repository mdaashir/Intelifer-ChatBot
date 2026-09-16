using Intellifer.Chatbot.Application.Dtos;

namespace Intellifer.Chatbot.Application.Interfaces;

public interface IChatBroadcaster
{
    Task BroadcastAsync(
        string connectionId,
        ChatMessageDto userMessage,
        ChatMessageDto botReply,
        CancellationToken cancellationToken = default);
}
