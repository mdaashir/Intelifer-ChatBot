using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Intellifer.Chatbot.Web.Hubs;

[Authorize]
public sealed class ChatHub : Hub<IChatHubClient>
{
    private readonly IChatMessageService _chatMessageService;
    private readonly IChatBroadcaster _chatBroadcaster;

    public ChatHub(IChatMessageService chatMessageService, IChatBroadcaster chatBroadcaster)
    {
        _chatMessageService = chatMessageService;
        _chatBroadcaster = chatBroadcaster;
    }

    public async Task SendMessage(string sessionId, string? content)
    {
        var userId = Context.User?.GetUserId();

        if (userId is null)
        {
            await Clients.Caller.ReceiveError("You must be signed in to send a message.");

            return;
        }

        if (!Guid.TryParse(sessionId, out var parsedSessionId))
        {
            await Clients.Caller.ReceiveError("A valid session id is required.");

            return;
        }

        var cancellationToken = Context.ConnectionAborted;

        var result = await _chatMessageService.SaveAndRespondAsync(
            userId,
            parsedSessionId,
            content,
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            await Clients.Caller.ReceiveError(string.Join(" ", result.Errors));

            return;
        }

        await _chatBroadcaster.BroadcastAsync(
            Context.ConnectionId,
            result.Value.UserMessage,
            result.Value.BotReply,
            cancellationToken);
    }
}
