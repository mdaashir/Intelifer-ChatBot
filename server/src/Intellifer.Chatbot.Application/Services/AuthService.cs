using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;

namespace Intellifer.Chatbot.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserManagementService _userManagement;
    private readonly ITokenService _tokenService;

    public AuthService(IUserManagementService userManagement, ITokenService tokenService)
    {
        _userManagement = userManagement;
        _tokenService = tokenService;
    }

    public async Task<OperationResult<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var registration = await _userManagement.RegisterAsync(request.Email, request.Password, cancellationToken);

        if (!registration.Succeeded)
        {
            return OperationResult<AuthResponse>.Failure(registration.Errors);
        }

        return OperationResult<AuthResponse>.Success(CreateResponse(registration.Value!));
    }

    public async Task<OperationResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return OperationResult<AuthResponse>.Failure("Email and password are required.");
        }

        var user = await _userManagement.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);

        // Deliberately identical to the bad-password message so the response
        // never reveals whether an email address is registered.
        if (user is null)
        {
            return OperationResult<AuthResponse>.Failure("Invalid email or password.");
        }

        return OperationResult<AuthResponse>.Success(CreateResponse(user));
    }

    private AuthResponse CreateResponse(UserDto user)
    {
        var token = _tokenService.CreateAccessToken(user);

        return new AuthResponse(token.Value, token.ExpiresAtUtc, user);
    }
}
