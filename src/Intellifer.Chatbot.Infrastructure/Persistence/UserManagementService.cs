using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Application.Policies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Intellifer.Chatbot.Infrastructure.Persistence;

public sealed class UserManagementService : IUserManagementService
{
    private const int MaxEmailLength = 256;

    private readonly UserManager<AppUser> _userManager;
    private readonly ChatDbContext _context;
    private readonly IClock _clock;

    public UserManagementService(UserManager<AppUser> userManager, ChatDbContext context, IClock clock)
    {
        _userManager = userManager;
        _context = context;
        _clock = clock;
    }

    public Task<OperationResult<UserDto>> RegisterAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken = default) =>
        CreateAccountAsync(email, password, RoleNames.User, cancellationToken);

    public async Task<OperationResult<UserDto>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsKnownRole(request.Role))
        {
            return OperationResult<UserDto>.Failure($"Role must be one of: {string.Join(", ", RoleNames.All)}.");
        }

        return await CreateAccountAsync(request.Email, request.Password, request.Role!, cancellationToken);
    }

    public async Task<UserDto?> ValidateCredentialsAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());

        return user is not null && await _userManager.CheckPasswordAsync(user, password)
            ? await ToDtoAsync(user)
            : null;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        var rolesByUser = await BuildRoleLookupAsync(cancellationToken);

        return users
            .Select(user => new UserDto(
                user.Id,
                user.Email ?? string.Empty,
                rolesByUser.TryGetValue(user.Id, out var roles) ? roles : [],
                user.CreatedAtUtc))
            .ToList();
    }

    public async Task<UserDto?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user is null ? null : await ToDtoAsync(user);
    }

    public async Task<OperationResult<UserDto>> UpdateAsync(
        string userId,
        string requestingUserId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult<UserDto>.Failure("User not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return OperationResult<UserDto>.Failure("Email is required.");
        }

        if (!IsKnownRole(request.Role))
        {
            return OperationResult<UserDto>.Failure($"Role must be one of: {string.Join(", ", RoleNames.All)}.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentRole = currentRoles.FirstOrDefault() ?? RoleNames.User;
        var adminCount = (await _userManager.GetUsersInRoleAsync(RoleNames.Admin)).Count;

        var policy = AccountPolicy.CanChangeRole(userId, requestingUserId, currentRole, request.Role!, adminCount);
        if (!policy.Succeeded)
        {
            return OperationResult<UserDto>.Failure(policy.Errors);
        }

        var email = request.Email.Trim();

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _userManager.SetEmailAsync(user, email);
            if (!emailResult.Succeeded)
            {
                return OperationResult<UserDto>.Failure(Describe(emailResult));
            }

            var userNameResult = await _userManager.SetUserNameAsync(user, email);
            if (!userNameResult.Succeeded)
            {
                return OperationResult<UserDto>.Failure(Describe(userNameResult));
            }
        }

        if (!string.Equals(currentRole, request.Role, StringComparison.Ordinal))
        {
            var removed = await _userManager.RemoveFromRoleAsync(user, currentRole);
            if (!removed.Succeeded)
            {
                return OperationResult<UserDto>.Failure(Describe(removed));
            }

            var added = await _userManager.AddToRoleAsync(user, request.Role!);
            if (!added.Succeeded)
            {
                return OperationResult<UserDto>.Failure(Describe(added));
            }
        }

        return OperationResult<UserDto>.Success(await ToDtoAsync(user));
    }

    public async Task<OperationResult> DeleteAsync(
        string userId,
        string requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var adminCount = (await _userManager.GetUsersInRoleAsync(RoleNames.Admin)).Count;

        var policy = AccountPolicy.CanDeleteAccount(
            userId,
            requestingUserId,
            roles.Contains(RoleNames.Admin),
            adminCount);

        if (!policy.Succeeded)
        {
            return policy;
        }

        var deleted = await _userManager.DeleteAsync(user);

        return deleted.Succeeded ? OperationResult.Success() : OperationResult.Failure(Describe(deleted));
    }

    public async Task<OperationResult> ResetPasswordAsync(
        string userId,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return OperationResult.Failure("A new password is required.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await _userManager.ResetPasswordAsync(user, token, newPassword);

        return reset.Succeeded ? OperationResult.Success() : OperationResult.Failure(Describe(reset));
    }

    public async Task<OperationResult> ChangePasswordAsync(
        string userId,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return OperationResult.Failure("Current and new password are required.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        var changed = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        return changed.Succeeded ? OperationResult.Success() : OperationResult.Failure(Describe(changed));
    }

    private async Task<OperationResult<UserDto>> CreateAccountAsync(
        string? email,
        string? password,
        string role,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add("Email is required.");
        }
        else if (email.Trim().Length > MaxEmailLength)
        {
            errors.Add($"Email cannot exceed {MaxEmailLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required.");
        }

        if (errors.Count > 0)
        {
            return OperationResult<UserDto>.Failure(errors);
        }

        var normalizedEmail = email!.Trim();

        var user = new AppUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            CreatedAtUtc = _clock.UtcNow
        };

        var created = await _userManager.CreateAsync(user, password!);
        if (!created.Succeeded)
        {
            return OperationResult<UserDto>.Failure(Describe(created));
        }

        var roleAssigned = await _userManager.AddToRoleAsync(user, role);
        if (!roleAssigned.Succeeded)
        {
            // Don't leave a half-built, role-less account behind.
            await _userManager.DeleteAsync(user);

            return OperationResult<UserDto>.Failure(Describe(roleAssigned));
        }

        return OperationResult<UserDto>.Success(await ToDtoAsync(user));
    }

    private async Task<UserDto> ToDtoAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            roles.OrderBy(role => role, StringComparer.Ordinal).ToList(),
            user.CreatedAtUtc);
    }

    private async Task<Dictionary<string, IReadOnlyList<string>>> BuildRoleLookupAsync(
        CancellationToken cancellationToken)
    {
        var assignments = await (from userRole in _context.UserRoles.AsNoTracking()
                                 join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                                 select new { userRole.UserId, Name = role.Name })
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(assignment => assignment.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(assignment => assignment.Name!)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList());
    }

    private static bool IsKnownRole(string? role) =>
        !string.IsNullOrWhiteSpace(role) && RoleNames.All.Contains(role, StringComparer.Ordinal);

    private static IEnumerable<string> Describe(IdentityResult result) =>
        result.Errors.Select(error => error.Description);
}
