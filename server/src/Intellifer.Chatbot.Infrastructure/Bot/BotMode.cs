namespace Intellifer.Chatbot.Infrastructure.Bot;

public enum BotMode
{
    /// <summary>
    /// Use the AI when it is configured, otherwise the rules. If a configured AI call
    /// fails, answer with the rules rather than showing the visitor an error.
    /// </summary>
    Auto,

    /// <summary>Rules only. The AI is never called, which keeps runs deterministic.</summary>
    Rules,

    /// <summary>AI only. A failure surfaces instead of being papered over by the rules.</summary>
    Ai
}
