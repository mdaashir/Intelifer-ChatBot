using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;

namespace Intellifer.Chatbot.UnitTests.Fakes;

public sealed class FakeAiChatClient : IAiChatClient
{
    public string Reply { get; set; } = "ai reply";

    public Exception? Throws { get; set; }

    public List<AiChatRequest> Requests { get; } = [];

    public Task<string> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        return Throws is null ? Task.FromResult(Reply) : Task.FromException<string>(Throws);
    }
}
