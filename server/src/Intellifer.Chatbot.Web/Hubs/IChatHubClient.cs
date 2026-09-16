using Intellifer.Chatbot.Application.Dtos;

namespace Intellifer.Chatbot.Web.Hubs;

public interface IChatHubClient
{
    Task ReceiveMessage(ChatMessageDto message);

    Task ReceiveError(string error);
}
