namespace Intellifer.Chatbot.Application.Dtos;

public sealed record AiChatRequest(IReadOnlyList<AiChatMessage> Messages);
