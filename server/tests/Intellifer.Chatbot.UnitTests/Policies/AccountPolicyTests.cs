using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Policies;

namespace Intellifer.Chatbot.UnitTests.Policies;

public sealed class AccountPolicyTests
{
    [Fact]
    public void CannotDeleteYourOwnAccount()
    {
        var result = AccountPolicy.CanDeleteAccount("admin-2", "admin-2", targetIsAdmin: true, adminCount: 2);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void CannotDeleteTheLastAdministrator()
    {
        var result = AccountPolicy.CanDeleteAccount("admin-1", "admin-2", targetIsAdmin: true, adminCount: 1);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void CanDeleteAnAdministratorWhenAnotherOneRemains()
    {
        var result = AccountPolicy.CanDeleteAccount("admin-1", "admin-2", targetIsAdmin: true, adminCount: 2);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void CanDeleteARegularUser()
    {
        var result = AccountPolicy.CanDeleteAccount("user-1", "admin-1", targetIsAdmin: false, adminCount: 1);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void CannotRemoveYourOwnAdministratorRole()
    {
        var result = AccountPolicy.CanChangeRole(
            "admin-1", "admin-1", RoleNames.Admin, RoleNames.User, adminCount: 2);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void CannotDemoteTheLastAdministrator()
    {
        var result = AccountPolicy.CanChangeRole(
            "admin-1", "admin-2", RoleNames.Admin, RoleNames.User, adminCount: 1);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void CanDemoteAnAdministratorWhenAnotherOneRemains()
    {
        var result = AccountPolicy.CanChangeRole(
            "admin-1", "admin-2", RoleNames.Admin, RoleNames.User, adminCount: 2);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void CanPromoteARegularUserToAdministrator()
    {
        var result = AccountPolicy.CanChangeRole(
            "user-1", "admin-1", RoleNames.User, RoleNames.Admin, adminCount: 1);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void KeepingTheAdministratorRoleIsAlwaysAllowed()
    {
        var result = AccountPolicy.CanChangeRole(
            "admin-1", "admin-1", RoleNames.Admin, RoleNames.Admin, adminCount: 1);

        Assert.True(result.Succeeded);
    }
}
