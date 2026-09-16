using System.Net.Http.Headers;
using System.Net.Http.Json;
using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Intellifer.Chatbot.IntegrationTests;

[Collection(ChatbotCollection.Name)]
public sealed class ChatHubTests
{
    private const string Password = "Password123";

    private readonly ChatbotEnvironmentFixture _environment;

    public ChatHubTests(ChatbotEnvironmentFixture environment)
    {
        _environment = environment;
    }

    [Fact]
    public async Task SendMessage_RepliesToTheCallerWithTheMessageAndTheBotReply()
    {
        var (_, token) = await CreateUserClientAsync();
        await using var connection = CreateConnection(token);

        var received = new List<MessageResponse>();
        connection.On<MessageResponse>("ReceiveMessage", message => Add(received, message));

        await connection.StartAsync();

        var sessionId = Guid.NewGuid();
        await connection.InvokeAsync("SendMessage", sessionId.ToString(), "hello");

        await WaitForAsync(() => Count(received) >= 2);

        Assert.Equal(2, Count(received));

        Assert.Equal("hello", received[0].Content);
        Assert.Equal("User", received[0].Sender);

        Assert.Equal("Bot", received[1].Sender);
        Assert.True(received[1].Id > received[0].Id);

        Assert.All(received, message => Assert.Equal(sessionId, message.SessionId));
    }

    [Fact]
    public async Task SendMessage_DoesNotDeliverTheReplyToAnotherConnection()
    {
        var (_, token) = await CreateUserClientAsync();

        await using var sender = CreateConnection(token);
        await using var bystander = CreateConnection(token);

        var receivedBySender = new List<MessageResponse>();
        var receivedByBystander = new List<MessageResponse>();

        sender.On<MessageResponse>("ReceiveMessage", message => Add(receivedBySender, message));
        bystander.On<MessageResponse>("ReceiveMessage", message => Add(receivedByBystander, message));

        await sender.StartAsync();
        await bystander.StartAsync();

        // Let the second connection finish negotiating before the message is sent,
        // otherwise this negative assertion could pass for the wrong reason.
        await Task.Delay(500);

        await sender.InvokeAsync("SendMessage", Guid.NewGuid().ToString(), "hello");

        await WaitForAsync(() => Count(receivedBySender) >= 2);

        Assert.Equal(2, Count(receivedBySender));
        Assert.Empty(receivedByBystander);
    }

    [Fact]
    public async Task SendMessage_WithAnEmptyMessage_ReceivesAnErrorAndPersistsNothing()
    {
        var (client, token) = await CreateUserClientAsync();
        await using var connection = CreateConnection(token);

        var errors = new List<string>();
        connection.On<string>("ReceiveError", error => Add(errors, error));

        await connection.StartAsync();

        var sessionId = Guid.NewGuid();
        await connection.InvokeAsync("SendMessage", sessionId.ToString(), "   ");

        await WaitForAsync(() => Count(errors) >= 1);

        Assert.Contains("empty", Assert.Single(errors), StringComparison.OrdinalIgnoreCase);

        var history = await client.GetFromJsonAsync<List<MessageResponse>>($"/api/messages?sessionId={sessionId}");

        Assert.Empty(history!);
    }

    [Fact]
    public async Task TheConversationSurvivesAPageRefreshBecauseItIsPersisted()
    {
        var (_, token) = await CreateUserClientAsync();
        await using var connection = CreateConnection(token);

        var received = new List<MessageResponse>();
        connection.On<MessageResponse>("ReceiveMessage", message => Add(received, message));

        await connection.StartAsync();

        var sessionId = Guid.NewGuid();
        await connection.InvokeAsync("SendMessage", sessionId.ToString(), "hello");

        await WaitForAsync(() => Count(received) >= 2);

        // A brand new client, exactly what a page refresh produces.
        var reloaded = _environment.Api.CreateClient();
        reloaded.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var history = await reloaded.GetFromJsonAsync<List<MessageResponse>>($"/api/messages?sessionId={sessionId}");

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal("hello", history[0].Content);
        Assert.Equal("Bot", history[1].Sender);
    }

    [Fact]
    public async Task AnotherUserCannotReadAConversationByGuessingTheSessionId()
    {
        var (_, ownerToken) = await CreateUserClientAsync();
        await using var ownerConnection = CreateConnection(ownerToken);

        var received = new List<MessageResponse>();
        ownerConnection.On<MessageResponse>("ReceiveMessage", message => Add(received, message));

        await ownerConnection.StartAsync();

        var sessionId = Guid.NewGuid();
        await ownerConnection.InvokeAsync("SendMessage", sessionId.ToString(), "this is private");

        await WaitForAsync(() => Count(received) >= 2);

        var (intruderClient, _) = await CreateUserClientAsync();

        var history = await intruderClient.GetFromJsonAsync<List<MessageResponse>>($"/api/messages?sessionId={sessionId}");

        Assert.Empty(history!);
    }

    [Fact]
    public async Task TheHubRefusesAConnectionThatCarriesNoToken()
    {
        await using var connection = CreateConnection(token: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    private async Task<(HttpClient Client, string Token)> CreateUserClientAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        await _environment.Api.CreateUserAsync(email, Password, RoleNames.User);

        var client = _environment.Api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return (client, auth.AccessToken);
    }

    private HubConnection CreateConnection(string? token)
    {
        var url = $"{_environment.Api.Server.BaseAddress}hubs/chat";

        if (!string.IsNullOrEmpty(token))
        {
            // The browser client cannot put an Authorization header on a WebSocket
            // handshake, so it sends the token in the query string instead. This is
            // deliberately that same path, not the Authorization header the .NET
            // client would use by default.
            url += $"?access_token={Uri.EscapeDataString(token)}";
        }

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _environment.Api.Server.CreateHandler();
            })
            .Build();
    }

    private static void Add<T>(List<T> target, T item)
    {
        lock (target)
        {
            target.Add(item);
        }
    }

    private static int Count<T>(List<T> source)
    {
        lock (source)
        {
            return source.Count;
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 15_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The expected condition was not met before the timeout elapsed.");
    }

    private sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc);

    private sealed record MessageResponse(
        int Id,
        Guid SessionId,
        string Content,
        string Sender,
        DateTime CreatedAtUtc);
}
