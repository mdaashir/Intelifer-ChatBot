using Intellifer.Chatbot.Application.Interfaces;

namespace Intellifer.Chatbot.Application.Services;

public sealed class RuleBasedBotResponder : IBotResponder
{
    private const string FallbackReply =
        "I'm a rule-based bot, so I only understand a handful of things for now. Try \"help\" to see what I can answer.";

    private static readonly char[] Separators =
        [' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '\'', '"', '(', ')', '[', ']', '-', '_', '*'];

    private static readonly BotRule[] Rules =
    [
        new(["how are you", "how're you", "how do you do"], [],
            "I'm running fine, thanks for asking. What can I help you with?"),

        new(["who are you", "what are you", "your name"], [],
            "I'm the Intellifer assistant - a small rule-based bot standing in for a real language model."),

        new(["what can you do", "help me"], ["help", "options", "commands"],
            "I can greet you, say who I am, and answer a few simple things. Try \"hello\", \"who are you\", \"thanks\" or \"bye\"."),

        new([], ["hi", "hello", "hey", "howdy", "greetings", "yo"],
            "Hello! I'm the Intellifer assistant. How can I help you today?"),

        new([], ["thanks", "thank", "thankyou", "cheers", "ta"],
            "You're welcome!"),

        new([], ["bye", "goodbye", "farewell", "cya", "later"],
            "Goodbye! Refresh the page whenever you like - I'll still remember this conversation.")
    ];

    public Task<string> GetReplyAsync(string content, CancellationToken cancellationToken = default)
    {
        var normalized = content.ToLowerInvariant();
        var words = normalized.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var rule in Rules)
        {
            var phraseMatched = rule.Phrases.Any(phrase => normalized.Contains(phrase, StringComparison.Ordinal));
            var wordMatched = rule.Words.Any(word => words.Contains(word, StringComparer.Ordinal));

            if (phraseMatched || wordMatched)
            {
                return Task.FromResult(rule.Reply);
            }
        }

        return Task.FromResult(FallbackReply);
    }

    private sealed record BotRule(string[] Phrases, string[] Words, string Reply);
}
