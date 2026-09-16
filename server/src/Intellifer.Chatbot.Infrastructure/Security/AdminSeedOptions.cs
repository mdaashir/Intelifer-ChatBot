namespace Intellifer.Chatbot.Infrastructure.Security;

public sealed class AdminSeedOptions
{
    public const string SectionName = "SeedAdmin";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
