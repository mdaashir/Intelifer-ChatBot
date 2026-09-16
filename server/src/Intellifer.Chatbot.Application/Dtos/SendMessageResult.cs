namespace Intellifer.Chatbot.Application.Dtos;

public sealed record SendMessageResult(ChatMessageDto UserMessage, ChatMessageDto BotReply);
