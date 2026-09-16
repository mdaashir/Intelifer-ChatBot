namespace Intellifer.Chatbot.Application.Dtos;

public sealed record UserDto(string Id, string Email, IReadOnlyList<string> Roles, DateTime CreatedAtUtc);
