using Intellifer.Chatbot.Application.Dtos;

namespace Intellifer.Chatbot.Application.Interfaces;

public interface ITokenService
{
    AccessToken CreateAccessToken(UserDto user);
}
