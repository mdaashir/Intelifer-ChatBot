using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;

namespace Intellifer.Chatbot.UnitTests.Fakes;

public sealed class FakeTokenService : ITokenService
{
    public DateTime ExpiresAtUtc { get; set; } = new(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);

    public UserDto? LastIssuedTo { get; private set; }

    public AccessToken CreateAccessToken(UserDto user)
    {
        LastIssuedTo = user;

        return new AccessToken("test-access-token", ExpiresAtUtc);
    }
}
