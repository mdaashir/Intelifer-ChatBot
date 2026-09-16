using Intellifer.Chatbot.Domain.Enums;

namespace Intellifer.Chatbot.Application.Dtos;

public sealed record ChatMessageDto(
    int Id,
    Guid SessionId,
    string Content,
    MessageSender Sender,
    DateTime CreatedAtUtc);
