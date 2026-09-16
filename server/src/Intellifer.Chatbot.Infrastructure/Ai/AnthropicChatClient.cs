using System.Text.Json;
using System.Text.Json.Serialization;
using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Microsoft.Extensions.Options;

namespace Intellifer.Chatbot.Infrastructure.Ai;

/// <summary>
/// Speaks the Anthropic Messages format, which differs from OpenAI in three ways that
/// matter here: the key travels in <c>x-api-key</c> rather than a bearer token, a
/// version header is required, and there is no system role - the system prompt is a
/// top-level field while the conversation only carries user and assistant turns.
/// </summary>
public sealed class AnthropicChatClient : AiChatClientBase
{
    private const string ApiVersion = "2023-06-01";

    public AnthropicChatClient(HttpClient httpClient, IOptions<AiChatClientOptions> options)
        : base(httpClient, options)
    {
    }

    protected override string ProviderName => "Anthropic";

    protected override string Path => "v1/messages";

    protected override HttpRequestMessage CreateRequest(AiChatRequest request)
    {
        var system = string.Join(
            "\n\n",
            request.Messages
                .Where(message => message.Role == AiChatRole.System)
                .Select(message => message.Content));

        var conversation = request.Messages
            .Where(message => message.Role != AiChatRole.System)
            .Select(message => new AnthropicMessage(
                message.Role == AiChatRole.Assistant ? "assistant" : "user",
                message.Content))
            .ToList();

        var payload = new AnthropicRequest(
            Options.Model,
            Options.MaxTokens,
            string.IsNullOrWhiteSpace(system) ? null : system,
            conversation);

        var message = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint())
        {
            Content = CreateJsonContent(payload)
        };

        message.Headers.Add("x-api-key", Options.ApiKey);
        message.Headers.Add("anthropic-version", ApiVersion);

        return message;
    }

    protected override string ParseContent(string body)
    {
        AnthropicResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<AnthropicResponse>(body, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new AiChatClientException("Anthropic returned a response that could not be read.", exception);
        }

        return response?.Content is null
            ? string.Empty
            : string.Concat(response.Content.Select(block => block.Text));
    }

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record AnthropicRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? System,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("text")] string? Text);

    private sealed record AnthropicResponse(
        [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContent>? Content);
}
