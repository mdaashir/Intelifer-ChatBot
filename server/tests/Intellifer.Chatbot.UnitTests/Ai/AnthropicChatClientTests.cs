using System.Net;
using System.Text;
using System.Text.Json;
using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Infrastructure.Ai;
using Intellifer.Chatbot.UnitTests.Fakes;
using Microsoft.Extensions.Options;

namespace Intellifer.Chatbot.UnitTests.Ai;

public sealed class AnthropicChatClientTests
{
    private const string SuccessBody = """
        {
          "content": [
            { "type": "text", "text": " the " },
            { "type": "text", "text": "answer " }
          ]
        }
        """;

    [Fact]
    public async Task PostsToTheMessagesEndpointOfTheConfiguredBaseUrl()
    {
        var (client, handler) = CreateClient();

        await client.CompleteAsync(CreateRequest());

        var sent = Assert.Single(handler.Requests);

        Assert.Equal("POST", sent.Method);
        Assert.Equal("https://api.anthropic.com/v1/messages", sent.Uri!.ToString());
    }

    [Fact]
    public async Task AuthenticatesWithTheApiKeyHeaderAndAVersionHeader()
    {
        var (client, handler) = CreateClient();

        await client.CompleteAsync(CreateRequest());

        var headers = Assert.Single(handler.Requests).Headers;

        Assert.Equal("test-key", headers["x-api-key"]);
        Assert.Equal("2023-06-01", headers["anthropic-version"]);

        // Anthropic does not accept a bearer token.
        Assert.False(headers.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task HoistsTheSystemMessageOutOfTheConversation()
    {
        var (client, handler) = CreateClient();

        await client.CompleteAsync(CreateRequest());

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);

        Assert.Equal("You are terse.", body.RootElement.GetProperty("system").GetString());

        var messages = body.RootElement.GetProperty("messages");

        Assert.Equal(3, messages.GetArrayLength());
        Assert.Equal(
            new[] { "user", "assistant", "user" },
            messages.EnumerateArray().Select(message => message.GetProperty("role").GetString()));
    }

    [Fact]
    public async Task OmitsTheSystemFieldWhenThereIsNoSystemMessage()
    {
        var (client, handler) = CreateClient();

        await client.CompleteAsync(new AiChatRequest([new AiChatMessage(AiChatRole.User, "hello")]));

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);

        Assert.False(body.RootElement.TryGetProperty("system", out _));
    }

    [Fact]
    public async Task JoinsEveryTextBlockAndTrimsTheResult()
    {
        var (client, _) = CreateClient();

        var reply = await client.CompleteAsync(CreateRequest());

        Assert.Equal("the answer", reply);
    }

    [Fact]
    public async Task ThrowsWhenTheProviderReturnsAnErrorStatus()
    {
        var (client, _) = CreateClient(
            """{"error":{"message":"overloaded"}}""",
            HttpStatusCode.TooManyRequests);

        var exception = await Assert.ThrowsAsync<AiChatClientException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Equal(429, exception.StatusCode);
        Assert.Contains("overloaded", exception.Message);
    }

    private static AiChatRequest CreateRequest() => new(
    [
        new AiChatMessage(AiChatRole.System, "You are terse."),
        new AiChatMessage(AiChatRole.User, "hello"),
        new AiChatMessage(AiChatRole.Assistant, "hi"),
        new AiChatMessage(AiChatRole.User, "how are you")
    ]);

    private static (AnthropicChatClient Client, StubHttpMessageHandler Handler) CreateClient(
        string responseBody = SuccessBody,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new StubHttpMessageHandler(() => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });

        var options = new AiChatClientOptions
        {
            Provider = AiProvider.Anthropic,
            BaseUrl = "https://api.anthropic.com",
            ApiKey = "test-key",
            Model = "claude-3-5-haiku-latest",
            MaxTokens = 128,
            TimeoutSeconds = 5
        };

        return (new AnthropicChatClient(new HttpClient(handler), Options.Create(options)), handler);
    }
}
