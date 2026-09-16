using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Intellifer.Chatbot.Infrastructure.Bot;

/// <summary>
/// Answers using a language model. The conversation the application hands over is
/// translated into the provider-agnostic message list the AI client expects.
/// </summary>
public sealed class AiBotResponder : IBotResponder
{
    private readonly IAiChatClient _aiChatClient;
    private readonly BotOptions _options;

    public AiBotResponder(IAiChatClient aiChatClient, IOptions<BotOptions> options)
    {
        _aiChatClient = aiChatClient;
        _options = options.Value;
    }

    public Task<string> GetReplyAsync(
        BotConversation conversation,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<AiChatMessage>
        {
            new(AiChatRole.System, _options.SystemPrompt)
        };

        messages.AddRange(BuildHistory(conversation.History));
        messages.Add(new AiChatMessage(AiChatRole.User, conversation.Prompt));

        return _aiChatClient.CompleteAsync(new AiChatRequest(messages), cancellationToken);
    }

    private IEnumerable<AiChatMessage> BuildHistory(IReadOnlyList<BotTurn> history)
    {
        var limit = _options.HistoryMessages;

        if (limit <= 0 || history.Count == 0)
        {
            return [];
        }

        // Keep the most recent turns: the tail of the conversation is what the reply
        // actually depends on.
        IReadOnlyList<BotTurn> recent = history.Count <= limit
            ? history
            : history.Skip(history.Count - limit).ToList();

        var messages = new List<AiChatMessage>();

        foreach (var turn in recent)
        {
            var role = turn.Sender == MessageSender.Bot ? AiChatRole.Assistant : AiChatRole.User;

            // A reply that failed to persist leaves two user turns in a row, and some
            // providers reject a conversation that does not alternate.
            if (messages.Count > 0 && messages[^1].Role == role)
            {
                messages[^1] = messages[^1] with { Content = $"{messages[^1].Content}\n{turn.Content}" };

                continue;
            }

            messages.Add(new AiChatMessage(role, turn.Content));
        }

        return messages;
    }
}
