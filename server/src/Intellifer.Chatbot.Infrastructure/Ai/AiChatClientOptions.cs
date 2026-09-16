namespace Intellifer.Chatbot.Infrastructure.Ai;

/// <summary>
/// Transport settings for whichever provider is selected. Deliberately separate from
/// the bot's own behaviour settings, so this class only ever describes how to reach
/// an endpoint.
/// </summary>
public sealed class AiChatClientOptions
{
    public const string SectionName = "Bot:Ai";

    public AiProvider Provider { get; set; } = AiProvider.OpenAi;

    /// <summary>
    /// Base address only, no path. For OpenAI that is typically
    /// <c>https://api.openai.com/v1</c>; for Anthropic, <c>https://api.anthropic.com</c>.
    /// Point it at a local Ollama or LM Studio instance to run entirely offline.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";

    public int MaxTokens { get; set; } = 512;

    public int TimeoutSeconds { get; set; } = 15;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model);
}
