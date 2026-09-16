using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;

namespace Intellifer.Chatbot.Application.Interfaces;

/// <summary>
/// A provider-agnostic chat completion. Adapters map this onto whichever wire format
/// the configured provider speaks and normalise every transport, protocol and parsing
/// failure into <see cref="AiChatClientException"/>, so the caller only has one
/// failure to reason about.
/// </summary>
public interface IAiChatClient
{
    Task<string> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}
