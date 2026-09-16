namespace Intellifer.Chatbot.Application.Dtos;

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
