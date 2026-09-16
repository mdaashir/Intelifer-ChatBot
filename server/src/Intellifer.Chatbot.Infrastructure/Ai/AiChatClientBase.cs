using System.Text;
using System.Text.Json;
using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Intellifer.Chatbot.Infrastructure.Ai;

/// <summary>
/// Everything the two providers share: configuration checking, sending, and turning
/// transport and protocol failures into <see cref="AiChatClientException"/>. A derived
/// client only has to describe its own wire format.
/// </summary>
public abstract class AiChatClientBase : IAiChatClient
{
    protected static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected AiChatClientBase(HttpClient httpClient, IOptions<AiChatClientOptions> options)
    {
        Options = options.Value;
        HttpClient = httpClient;
        HttpClient.Timeout = TimeSpan.FromSeconds(Options.TimeoutSeconds);
    }

    protected AiChatClientOptions Options { get; }

    protected HttpClient HttpClient { get; }

    protected abstract string ProviderName { get; }

    /// <summary>Path appended to the configured base address, without a leading slash.</summary>
    protected abstract string Path { get; }

    public async Task<string> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            throw new AiChatClientException(
                $"The {ProviderName} provider is not configured. Set {AiChatClientOptions.SectionName}:BaseUrl and :ApiKey.");
        }

        using var message = CreateRequest(request);

        var body = await SendAsync(message, cancellationToken);
        var content = ParseContent(body);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiChatClientException($"{ProviderName} returned an empty reply.");
        }

        return content.Trim();
    }

    protected abstract HttpRequestMessage CreateRequest(AiChatRequest request);

    protected abstract string ParseContent(string body);

    protected Uri BuildEndpoint() => new($"{Options.BaseUrl.TrimEnd('/')}/{Path}");

    protected static StringContent CreateJsonContent<T>(T payload) =>
        new(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

    private async Task<string> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await HttpClient.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiChatClientException(
                $"The {ProviderName} request timed out after {Options.TimeoutSeconds}s.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiChatClientException($"The {ProviderName} request could not be sent.", exception);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;

                throw new AiChatClientException(
                    $"{ProviderName} returned {statusCode}: {Truncate(body)}",
                    statusCode);
            }

            return body;
        }
    }

    private static string Truncate(string body) =>
        body.Length <= 300 ? body : $"{body[..300]}...";
}
