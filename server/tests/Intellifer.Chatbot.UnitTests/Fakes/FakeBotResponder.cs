using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;

namespace Intellifer.Chatbot.UnitTests.Fakes;

public sealed class FakeBotResponder : IBotResponder
{
    public string Reply { get; set; } = "bot reply";

    public Exception? Throws { get; set; }

    public List<BotConversation> ReceivedConversations { get; } = [];

    public Task<string> GetReplyAsync(
        BotConversation conversation,
        CancellationToken cancellationToken = default)
    {
        ReceivedConversations.Add(conversation);

        return Throws is null ? Task.FromResult(Reply) : Task.FromException<string>(Throws);
    }
}
