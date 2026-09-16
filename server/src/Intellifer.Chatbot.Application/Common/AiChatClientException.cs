namespace Intellifer.Chatbot.Application.Common;

/// <summary>
/// The single failure type an <c>IAiChatClient</c> is allowed to raise, so callers
/// have exactly one thing to catch and can treat it as "the provider let us down"
/// rather than having to reason about HTTP and JSON.
/// </summary>
public sealed class AiChatClientException : Exception
{
    public AiChatClientException(string message)
        : base(message)
    {
    }

    public AiChatClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public AiChatClientException(string message, int? statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
