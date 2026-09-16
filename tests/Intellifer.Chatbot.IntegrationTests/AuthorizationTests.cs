using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.IntegrationTests.Fixtures;

namespace Intellifer.Chatbot.IntegrationTests;

[Collection(ChatbotCollection.Name)]
public sealed class AuthorizationTests
{
    private const string Password = "Password123";

    private readonly ChatbotEnvironmentFixture _environment;

    public AuthorizationTests(ChatbotEnvironmentFixture environment)
    {
        _environment = environment;
    }

    [Fact]
    public async Task AdminEndpoint_WithoutAToken_IsUnauthorized()
    {
        var client = _environment.Api.CreateClient();

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WithARegularUserToken_IsForbidden()
    {
        var email = UniqueEmail();
        await _environment.Api.CreateUserAsync(email, Password, RoleNames.User);

        var client = await CreateAuthenticatedClientAsync(email);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WithAnAdminToken_IsAllowed()
    {
        var email = UniqueEmail();
        await _environment.Api.CreateUserAsync(email, Password, RoleNames.Admin);

        var client = await CreateAuthenticatedClientAsync(email);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HistoryEndpoint_WithoutAToken_IsUnauthorized()
    {
        var client = _environment.Api.CreateClient();

        var response = await client.GetAsync($"/api/messages?sessionId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeEndpoint_ReturnsTheSignedInUserWithRoles()
    {
        var email = UniqueEmail();
        await _environment.Api.CreateUserAsync(email, Password, RoleNames.User);

        var client = await CreateAuthenticatedClientAsync(email);

        var user = await client.GetFromJsonAsync<UserResponse>("/api/auth/me");

        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Contains(RoleNames.User, user.Roles);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_IsABadRequestNotAnUnauthorized()
    {
        var email = UniqueEmail();
        await _environment.Api.CreateUserAsync(email, Password, RoleNames.User);

        var client = _environment.Api.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "definitely-the-wrong-password" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ThenUseTheReturnedToken_ReachesAProtectedEndpoint()
    {
        var client = _environment.Api.CreateClient();
        var email = UniqueEmail();

        var registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password = Password });

        registration.EnsureSuccessStatusCode();

        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync($"/api/messages?sessionId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithAPasswordThatIsTooShort_IsRejected()
    {
        var client = _environment.Api.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = UniqueEmail(), password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisteredUsersCannotReachTheAdminEndpoint()
    {
        var client = _environment.Api.CreateClient();

        var registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = UniqueEmail(), password = Password });

        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = _environment.Api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc);

    private sealed record UserResponse(string Id, string Email, IReadOnlyList<string> Roles);
}
