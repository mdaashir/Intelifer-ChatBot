namespace Intellifer.Chatbot.Application.Dtos;

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);
