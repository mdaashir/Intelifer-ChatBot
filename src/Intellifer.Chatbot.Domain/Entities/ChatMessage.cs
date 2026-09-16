using Intellifer.Chatbot.Domain.Enums;

namespace Intellifer.Chatbot.Domain.Entities;

public class ChatMessage
{
    public const int MaxContentLength = 2000;

    public int Id { get; set; }

    public Guid SessionId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public MessageSender Sender { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
