namespace Intellifer.Chatbot.Infrastructure.Bot;

/// <summary>
/// How the bot behaves, as opposed to how it reaches a provider (see
/// <c>AiChatClientOptions</c> for that).
/// </summary>
public sealed class BotOptions
{
    public const string SectionName = "Bot";

    public BotMode Mode { get; set; } = BotMode.Auto;

    public string SystemPrompt { get; set; } =
        "You are the Intellifer assistant, a helpful and concise chatbot. Keep replies short and friendly.";

    /// <summary>
    /// How many prior turns to send to the model. Zero disables context entirely.
    /// </summary>
    public int HistoryMessages { get; set; } = 20;
}
