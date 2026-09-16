using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Services;
using Intellifer.Chatbot.UnitTests.Fakes;

namespace Intellifer.Chatbot.UnitTests.Services;

public sealed class AuthServiceTests
{
    private readonly FakeUserManagementService _userManagement = new();
    private readonly FakeTokenService _tokenService = new();

    [Fact]
    public async Task Register_WhenTheAccountIsCreated_ReturnsATokenForThatUser()
    {
        var result = await CreateService().RegisterAsync(new RegisterRequest("new@example.com", "Password123"));

        Assert.True(result.Succeeded);
        Assert.Equal("test-access-token", result.Value!.AccessToken);
        Assert.Equal(FakeUserManagementService.SampleUser, _tokenService.LastIssuedTo);
    }

    [Fact]
    public async Task Register_WhenTheAccountCannotBeCreated_PropagatesTheErrorsAndIssuesNoToken()
    {
        _userManagement.RegisterResult = OperationResult<UserDto>.Failure("Email 'taken@example.com' is already taken.");

        var result = await CreateService().RegisterAsync(new RegisterRequest("taken@example.com", "Password123"));

        Assert.False(result.Succeeded);
        Assert.Contains("Email 'taken@example.com' is already taken.", result.Errors);
        Assert.Null(_tokenService.LastIssuedTo);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsATokenAndTheUser()
    {
        _userManagement.CredentialsResult = FakeUserManagementService.SampleUser;

        var result = await CreateService().LoginAsync(new LoginRequest("user@example.com", "Password123"));

        Assert.True(result.Succeeded);
        Assert.Equal("test-access-token", result.Value!.AccessToken);
        Assert.Equal(FakeUserManagementService.SampleUser, result.Value.User);
    }

    [Fact]
    public async Task Login_WithBadCredentials_FailsWithoutRevealingWhetherTheAccountExists()
    {
        _userManagement.CredentialsResult = null;

        var result = await CreateService().LoginAsync(new LoginRequest("user@example.com", "wrong-password"));

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid email or password.", Assert.Single(result.Errors));
    }

    [Theory]
    [InlineData(null, "Password123")]
    [InlineData("user@example.com", null)]
    [InlineData("", "")]
    [InlineData("   ", "Password123")]
    public async Task Login_WithoutBothFields_FailsWithoutTouchingTheUserStore(string? email, string? password)
    {
        var result = await CreateService().LoginAsync(new LoginRequest(email, password));

        Assert.False(result.Succeeded);
        Assert.Null(_userManagement.LastValidatedEmail);
    }

    private AuthService CreateService() => new(_userManagement, _tokenService);
}
