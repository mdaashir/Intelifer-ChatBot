using Intellifer.Chatbot.Application.Dtos;

namespace Intellifer.Chatbot.Application.Interfaces;

public interface IBotResponder
{
    Task<string> GetReplyAsync(
        BotConversation conversation,
        CancellationToken cancellationToken = default);
}
