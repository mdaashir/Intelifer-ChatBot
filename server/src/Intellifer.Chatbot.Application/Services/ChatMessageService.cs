using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Domain.Entities;
using Intellifer.Chatbot.Domain.Enums;

namespace Intellifer.Chatbot.Application.Services;

public sealed class ChatMessageService : IChatMessageService
{
    private readonly IChatRepository _repository;
    private readonly IBotResponder _botResponder;
    private readonly IClock _clock;

    public ChatMessageService(IChatRepository repository, IBotResponder botResponder, IClock clock)
    {
        _repository = repository;
        _botResponder = botResponder;
        _clock = clock;
    }

    public async Task<OperationResult<SendMessageResult>> SaveAndRespondAsync(
        string userId,
        Guid sessionId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(userId, sessionId, content);
        if (!validation.Succeeded)
        {
            return OperationResult<SendMessageResult>.Failure(validation.Errors);
        }

        var trimmed = content!.Trim();

        // Read the context before persisting the new message, so what the responder
        // receives as history is genuinely what came before the prompt.
        var priorTurns = await _repository.GetRecentAsync(
            sessionId,
            userId,
            BotConversation.MaxHistoryTurns,
            cancellationToken);

        // Saved one at a time and in order so the identity column guarantees the
        // reply always sorts after the message that prompted it.
        var userMessage = new ChatMessage
        {
            SessionId = sessionId,
            UserId = userId,
            Sender = MessageSender.User,
            Content = trimmed,
            CreatedAtUtc = _clock.UtcNow
        };

        await _repository.AddAsync(userMessage, cancellationToken);

        var conversation = new BotConversation(
            trimmed,
            priorTurns.Select(turn => new BotTurn(turn.Sender, turn.Content)).ToList());

        var reply = await _botResponder.GetReplyAsync(conversation, cancellationToken);

        var botMessage = new ChatMessage
        {
            SessionId = sessionId,
            UserId = userId,
            Sender = MessageSender.Bot,
            Content = reply,
            CreatedAtUtc = _clock.UtcNow
        };

        await _repository.AddAsync(botMessage, cancellationToken);

        return OperationResult<SendMessageResult>.Success(
            new SendMessageResult(ToDto(userMessage), ToDto(botMessage)));
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetHistoryAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || sessionId == Guid.Empty)
        {
            return [];
        }

        var messages = await _repository.GetBySessionAsync(sessionId, userId, cancellationToken);

        return messages.Select(ToDto).ToList();
    }

    private static OperationResult Validate(string userId, Guid sessionId, string? content)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(userId))
        {
            errors.Add("A user is required to send a message.");
        }

        if (sessionId == Guid.Empty)
        {
            errors.Add("A valid session id is required.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add("A message cannot be empty.");
        }
        else if (content.Trim().Length > ChatMessage.MaxContentLength)
        {
            errors.Add($"A message cannot exceed {ChatMessage.MaxContentLength} characters.");
        }

        return errors.Count == 0 ? OperationResult.Success() : OperationResult.Failure(errors);
    }

    private static ChatMessageDto ToDto(ChatMessage message) =>
        new(message.Id, message.SessionId, message.Content, message.Sender, message.CreatedAtUtc);
}
