using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Intellifer.Chatbot.Infrastructure.Persistence;

public sealed class ChatRepository : IChatRepository
{
    private readonly ChatDbContext _context;

    public ChatRepository(ChatDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        _context.ChatMessages.Add(message);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
        Guid sessionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId && message.UserId == userId)
            .OrderBy(message => message.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentAsync(
        Guid sessionId,
        string userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        // Read newest first so the Take can stop early, then flip back to
        // chronological order for the caller.
        var recent = await _context.ChatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId && message.UserId == userId)
            .OrderByDescending(message => message.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

        recent.Reverse();

        return recent;
    }
}
