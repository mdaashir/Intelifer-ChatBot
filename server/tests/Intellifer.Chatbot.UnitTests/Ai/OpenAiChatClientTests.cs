using System.Net;
using System.Text;
using System.Text.Json;
using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Infrastructure.Ai;
using Intellifer.Chatbot.UnitTests.Fakes;
using Microsoft.Extensions.Options;

namespace Intellifer.Chatbot.UnitTests.Ai;

public sealed class OpenAiChatClientTests
{
    private const string SuccessBody = """
        {
          "choices": [
            { "message": { "role": "assistant", "content": "  the answer  " } }
          ]
        }
        """;

    [Fact]
    public async Task PostsToTheChatCompletionsEndpointOfTheConfiguredBaseUrl()
    {
        var (client, handler) = CreateClient();

        await client.CompleteAsync(CreateRequest());

        var sent = Assert.Single(handler.Requests);

        Assert.Equal("POST", sent.Method);
        Assert.Equal("https://api.example.com/v1/chat/completions", sent.Uri!.ToString());
    }

    [Fact]
    public async Task SendsTheKeyAsABearerToken()
    {
        var (client, handler) = CreateClient();

        await client.CompleteAsync(CreateRequest());

        Assert.Equal("Bearer test-key", Assert.Single(handler.Requests).Headers["Authorization"]);
    }

    [Fact]
    public async Task SendsTheModelMessagesAndMaxTokens()
    {
        var (client, handler) = CreateClient();

        await client.CompleteAsync(CreateRequest());

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);

        Assert.Equal("test-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(128, body.RootElement.GetProperty("max_tokens").GetInt32());

        var messages = body.RootElement.GetProperty("messages");

        Assert.Equal(4, messages.GetArrayLength());
        Assert.Equal(
            new[] { "system", "user", "assistant", "user" },
            messages.EnumerateArray().Select(message => message.GetProperty("role").GetString()));
        Assert.Equal(
            new[] { "You are terse.", "hello", "hi", "how are you" },
            messages.EnumerateArray().Select(message => message.GetProperty("content").GetString()));
    }

    [Fact]
    public async Task ReturnsTheAssistantReplyTrimmed()
    {
        var (client, _) = CreateClient();

        var reply = await client.CompleteAsync(CreateRequest());

        Assert.Equal("the answer", reply);
    }

    [Fact]
    public async Task ThrowsWhenTheProviderReturnsAnErrorStatus()
    {
        var (client, _) = CreateClient(
            """{"error":{"message":"bad key"}}""",
            HttpStatusCode.Unauthorized);

        var exception = await Assert.ThrowsAsync<AiChatClientException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Equal(401, exception.StatusCode);
        Assert.Contains("bad key", exception.Message);
    }

    [Fact]
    public async Task ThrowsWhenTheReplyIsEmpty()
    {
        var (client, _) = CreateClient("""{"choices":[{"message":{"content":"   "}}]}""");

        await Assert.ThrowsAsync<AiChatClientException>(() => client.CompleteAsync(CreateRequest()));
    }

    [Fact]
    public async Task ThrowsWhenTheResponseCannotBeParsed()
    {
        var (client, _) = CreateClient("not json at all");

        await Assert.ThrowsAsync<AiChatClientException>(() => client.CompleteAsync(CreateRequest()));
    }

    [Fact]
    public async Task FailsWithoutSendingWhenTheProviderIsNotConfigured()
    {
        var (client, handler) = CreateClient(configure: options => options.BaseUrl = string.Empty);

        var exception = await Assert.ThrowsAsync<AiChatClientException>(
            () => client.CompleteAsync(CreateRequest()));

        Assert.Contains("not configured", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AppendsThePathEvenWhenTheBaseUrlHasATrailingSlash()
    {
        var (client, handler) = CreateClient(configure: options => options.BaseUrl = "http://localhost:11434/v1/");

        await client.CompleteAsync(CreateRequest());

        Assert.Equal("http://localhost:11434/v1/chat/completions", Assert.Single(handler.Requests).Uri!.ToString());
    }

    private static AiChatRequest CreateRequest() => new(
    [
        new AiChatMessage(AiChatRole.System, "You are terse."),
        new AiChatMessage(AiChatRole.User, "hello"),
        new AiChatMessage(AiChatRole.Assistant, "hi"),
        new AiChatMessage(AiChatRole.User, "how are you")
    ]);

    private static (OpenAiChatClient Client, StubHttpMessageHandler Handler) CreateClient(
        string responseBody = SuccessBody,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        Action<AiChatClientOptions>? configure = null)
    {
        var handler = new StubHttpMessageHandler(() => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });

        var options = new AiChatClientOptions
        {
            Provider = AiProvider.OpenAi,
            BaseUrl = "https://api.example.com/v1",
            ApiKey = "test-key",
            Model = "test-model",
            MaxTokens = 128,
            TimeoutSeconds = 5
        };

        configure?.Invoke(options);

        return (new OpenAiChatClient(new HttpClient(handler), Options.Create(options)), handler);
    }
}
