using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;

namespace Intellifer.Chatbot.Application.Interfaces;

public interface IAuthService
{
    Task<OperationResult<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
