using Intellifer.Chatbot.Application.Common;

namespace Intellifer.Chatbot.Application.Policies;

public static class AccountPolicy
{
    public static OperationResult CanDeleteAccount(
        string targetUserId,
        string requestingUserId,
        bool targetIsAdmin,
        int adminCount)
    {
        if (string.Equals(targetUserId, requestingUserId, StringComparison.Ordinal))
        {
            return OperationResult.Failure("You cannot delete your own account.");
        }

        if (targetIsAdmin && adminCount <= 1)
        {
            return OperationResult.Failure("The last administrator account cannot be deleted.");
        }

        return OperationResult.Success();
    }

    public static OperationResult CanChangeRole(
        string targetUserId,
        string requestingUserId,
        string currentRole,
        string newRole,
        int adminCount)
    {
        var isCurrentlyAdmin = string.Equals(currentRole, RoleNames.Admin, StringComparison.Ordinal);
        var willRemainAdmin = string.Equals(newRole, RoleNames.Admin, StringComparison.Ordinal);

        if (!isCurrentlyAdmin || willRemainAdmin)
        {
            return OperationResult.Success();
        }

        if (string.Equals(targetUserId, requestingUserId, StringComparison.Ordinal))
        {
            return OperationResult.Failure("You cannot remove your own administrator role.");
        }

        if (adminCount <= 1)
        {
            return OperationResult.Failure("The last administrator cannot be demoted.");
        }

        return OperationResult.Success();
    }
}
