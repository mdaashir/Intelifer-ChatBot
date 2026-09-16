namespace Intellifer.Chatbot.Application.Interfaces;

public interface IBotResponder
{
    Task<string> GetReplyAsync(string content, CancellationToken cancellationToken = default);
}
