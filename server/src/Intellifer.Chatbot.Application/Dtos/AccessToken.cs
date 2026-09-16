namespace Intellifer.Chatbot.Application.Dtos;

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
