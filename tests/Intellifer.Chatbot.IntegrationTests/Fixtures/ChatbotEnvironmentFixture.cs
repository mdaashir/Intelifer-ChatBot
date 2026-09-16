using Intellifer.Chatbot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Intellifer.Chatbot.IntegrationTests.Fixtures;

public sealed class ChatbotEnvironmentFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private ChatbotApiFactory? _factory;

    public ChatbotApiFactory Api =>
        _factory ?? throw new InvalidOperationException("The fixture has not been initialized.");

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new ChatbotApiFactory(_container.GetConnectionString());

        await _factory.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public ChatDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        return new ChatDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class ChatbotCollection : ICollectionFixture<ChatbotEnvironmentFixture>
{
    public const string Name = "chatbot-environment";
}
