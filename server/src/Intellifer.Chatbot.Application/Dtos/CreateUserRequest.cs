namespace Intellifer.Chatbot.Application.Dtos;

public sealed record CreateUserRequest(string? Email, string? Password, string? Role);
