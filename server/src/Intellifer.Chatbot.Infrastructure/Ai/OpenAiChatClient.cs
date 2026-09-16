using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Microsoft.Extensions.Options;

namespace Intellifer.Chatbot.Infrastructure.Ai;

/// <summary>
/// Speaks the OpenAI chat completions format. Because that format is the de-facto
/// standard, the same client also covers Azure OpenAI, OpenRouter, Groq, Together,
/// vLLM, LM Studio and Ollama's OpenAI-compatible endpoint - only BaseUrl differs.
/// </summary>
public sealed class OpenAiChatClient : AiChatClientBase
{
    public OpenAiChatClient(HttpClient httpClient, IOptions<AiChatClientOptions> options)
        : base(httpClient, options)
    {
    }

    protected override string ProviderName => "OpenAI";

    protected override string Path => "chat/completions";

    protected override HttpRequestMessage CreateRequest(AiChatRequest request)
    {
        var payload = new OpenAiRequest(
            Options.Model,
            request.Messages
                .Select(message => new OpenAiMessage(ToWireRole(message.Role), message.Content))
                .ToList(),
            Options.MaxTokens);

        var message = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint())
        {
            Content = CreateJsonContent(payload)
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.ApiKey);

        return message;
    }

    protected override string ParseContent(string body)
    {
        OpenAiResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<OpenAiResponse>(body, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new AiChatClientException("OpenAI returned a response that could not be read.", exception);
        }

        return response?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    private static string ToWireRole(AiChatRole role) => role switch
    {
        AiChatRole.System => "system",
        AiChatRole.Assistant => "assistant",
        _ => "user"
    };

    private sealed record OpenAiMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenAiRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiMessage> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record OpenAiChoice(
        [property: JsonPropertyName("message")] OpenAiMessage? Message);

    private sealed record OpenAiResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChoice>? Choices);
}
