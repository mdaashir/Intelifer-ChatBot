using Intellifer.Chatbot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intellifer.Chatbot.IntegrationTests.Fixtures;

public sealed class ChatbotApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ChatbotApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();

        var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();

        await initializer.InitializeAsync();
    }

    public async Task<string> CreateUserAsync(string email, string password, string role)
    {
        using var scope = Services.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(user, password);

        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", created.Errors.Select(error => error.Description)));
        }

        await userManager.AddToRoleAsync(user, role);

        return user.Id;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["Jwt:Issuer"] = "Intellifer.Chatbot.IntegrationTests",
                ["Jwt:Audience"] = "Intellifer.Chatbot.IntegrationTests.Client",
                ["Jwt:SigningKey"] = "integration-test-signing-key-that-is-definitely-long-enough",
                ["SeedAdmin:Email"] = string.Empty,
                ["SeedAdmin:Password"] = string.Empty
            });
        });
    }
}
