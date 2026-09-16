using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Intellifer.Chatbot.Web.Hubs;

public sealed class ChatBroadcaster : IChatBroadcaster
{
    private readonly IHubContext<ChatHub, IChatHubClient> _hubContext;

    public ChatBroadcaster(IHubContext<ChatHub, IChatHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastAsync(
        string connectionId,
        ChatMessageDto userMessage,
        ChatMessageDto botReply,
        CancellationToken cancellationToken = default)
    {
        // Addressed to the originating connection rather than Clients.All, so a
        // reply is never delivered to another visitor. The connection id comes from
        // the hub because Caller/Others are only resolvable inside a hub method.
        var caller = _hubContext.Clients.Client(connectionId);

        await caller.ReceiveMessage(userMessage);
        await caller.ReceiveMessage(botReply);
    }
}
