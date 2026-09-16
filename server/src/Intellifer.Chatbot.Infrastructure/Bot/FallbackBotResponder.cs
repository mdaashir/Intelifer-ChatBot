using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Application.Services;
using Microsoft.Extensions.Logging;

namespace Intellifer.Chatbot.Infrastructure.Bot;

/// <summary>
/// Answers with <see cref="_primary"/> and falls back to <see cref="_fallback"/> when
/// the provider lets us down, so a bad key, a rate limit or a network blip degrades
/// into a rule-based answer instead of an error in the chat window.
/// </summary>
public sealed class FallbackBotResponder : IBotResponder
{
    private readonly IBotResponder _primary;
    private readonly IBotResponder _fallback;
    private readonly ILogger<FallbackBotResponder> _logger;

    public FallbackBotResponder(
        AiBotResponder primary,
        RuleBasedBotResponder fallback,
        ILogger<FallbackBotResponder> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<string> GetReplyAsync(
        BotConversation conversation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _primary.GetReplyAsync(conversation, cancellationToken);
        }
        catch (AiChatClientException exception)
        {
            // Only the provider's own failure type is caught. A bug in the responder
            // should surface as an error rather than be hidden behind a canned reply.
            _logger.LogWarning(
                exception,
                "The AI responder failed, so the rule-based responder answered instead.");

            return await _fallback.GetReplyAsync(conversation, cancellationToken);
        }
    }
}
