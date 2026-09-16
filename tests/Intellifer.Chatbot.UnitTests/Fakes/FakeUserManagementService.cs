using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;

namespace Intellifer.Chatbot.UnitTests.Fakes;

public sealed class FakeUserManagementService : IUserManagementService
{
    public static UserDto SampleUser { get; } = new(
        "user-1",
        "user@example.com",
        [RoleNames.User],
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    public OperationResult<UserDto> RegisterResult { get; set; } = OperationResult<UserDto>.Success(SampleUser);

    public UserDto? CredentialsResult { get; set; }

    public string? LastValidatedEmail { get; private set; }

    public Task<OperationResult<UserDto>> RegisterAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RegisterResult);

    public Task<UserDto?> ValidateCredentialsAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        LastValidatedEmail = email;

        return Task.FromResult(CredentialsResult);
    }

    public Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserDto>>([SampleUser]);

    public Task<UserDto?> GetByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<UserDto?>(SampleUser);

    public Task<OperationResult<UserDto>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RegisterResult);

    public Task<OperationResult<UserDto>> UpdateAsync(
        string userId,
        string requestingUserId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RegisterResult);

    public Task<OperationResult> DeleteAsync(
        string userId,
        string requestingUserId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult.Success());

    public Task<OperationResult> ResetPasswordAsync(
        string userId,
        string? newPassword,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult.Success());

    public Task<OperationResult> ChangePasswordAsync(
        string userId,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult.Success());
}
