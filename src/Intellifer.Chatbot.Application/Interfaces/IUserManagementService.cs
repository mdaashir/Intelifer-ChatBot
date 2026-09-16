using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;

namespace Intellifer.Chatbot.Application.Interfaces;

public interface IUserManagementService
{
    Task<OperationResult<UserDto>> RegisterAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken = default);

    Task<UserDto?> ValidateCredentialsAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserDto?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<OperationResult<UserDto>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult<UserDto>> UpdateAsync(
        string userId,
        string requestingUserId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteAsync(
        string userId,
        string requestingUserId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ResetPasswordAsync(
        string userId,
        string? newPassword,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ChangePasswordAsync(
        string userId,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken = default);
}
