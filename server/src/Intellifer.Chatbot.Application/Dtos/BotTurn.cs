using Intellifer.Chatbot.Domain.Enums;

namespace Intellifer.Chatbot.Application.Dtos;

public sealed record BotTurn(MessageSender Sender, string Content);
