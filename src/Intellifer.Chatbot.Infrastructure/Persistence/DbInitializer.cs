using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Intellifer.Chatbot.Infrastructure.Persistence;

public sealed class DbInitializer
{
    private readonly ChatDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AdminSeedOptions _adminOptions;
    private readonly IClock _clock;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        ChatDbContext context,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<AdminSeedOptions> adminOptions,
        IClock clock,
        ILogger<DbInitializer> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _adminOptions = adminOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.MigrateAsync(cancellationToken);

        await SeedRolesAsync();
        await SeedAdministratorAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in RoleNames.All)
        {
            if (await _roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var created = await _roleManager.CreateAsync(new IdentityRole(role));

            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed the '{role}' role: {Describe(created)}");
            }
        }
    }

    private async Task SeedAdministratorAsync()
    {
        if (string.IsNullOrWhiteSpace(_adminOptions.Email) || string.IsNullOrWhiteSpace(_adminOptions.Password))
        {
            _logger.LogWarning("No seed administrator is configured; skipping administrator creation.");

            return;
        }

        var email = _adminOptions.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(email);

        if (existing is not null)
        {
            if (!await _userManager.IsInRoleAsync(existing, RoleNames.Admin))
            {
                await _userManager.AddToRoleAsync(existing, RoleNames.Admin);
                _logger.LogInformation("Granted the administrator role to the existing account {Email}.", email);
            }

            return;
        }

        var administrator = new AppUser
        {
            UserName = email,
            Email = email,
            CreatedAtUtc = _clock.UtcNow
        };

        var created = await _userManager.CreateAsync(administrator, _adminOptions.Password);

        if (!created.Succeeded)
        {
            _logger.LogError(
                "Could not seed the administrator account {Email}: {Errors}",
                email,
                Describe(created));

            return;
        }

        await _userManager.AddToRoleAsync(administrator, RoleNames.Admin);

        _logger.LogInformation("Seeded the administrator account {Email}.", email);
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));
}
