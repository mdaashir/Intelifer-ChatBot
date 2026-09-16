using Intellifer.Chatbot.Application.Interfaces;

namespace Intellifer.Chatbot.UnitTests.Fakes;

public sealed class FakeBotResponder : IBotResponder
{
    public string Reply { get; set; } = "bot reply";

    public Exception? Throws { get; set; }

    public List<string> ReceivedContents { get; } = [];

    public Task<string> GetReplyAsync(string content, CancellationToken cancellationToken = default)
    {
        ReceivedContents.Add(content);

        return Throws is null ? Task.FromResult(Reply) : Task.FromException<string>(Throws);
    }
}
