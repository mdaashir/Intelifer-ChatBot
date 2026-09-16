using System.Text;
using System.Text.Json.Serialization;
using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Application.Services;
using Intellifer.Chatbot.Infrastructure.Ai;
using Intellifer.Chatbot.Infrastructure.Bot;
using Intellifer.Chatbot.Infrastructure.Persistence;
using Intellifer.Chatbot.Infrastructure.Security;
using Intellifer.Chatbot.Web.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------- options
// Configuration is bound to options and resolved through DI rather than read
// eagerly here, so the host (including a test host) can still replace any of it
// before the first resolution happens.
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "'Jwt:Issuer' is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "'Jwt:Audience' is required.")
    .Validate(
        options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
        "'Jwt:SigningKey' must be at least 32 bytes so HMAC-SHA256 has enough key material.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AdminSeedOptions>()
    .Bind(builder.Configuration.GetSection(AdminSeedOptions.SectionName));

// ---------------------------------------------------------------- persistence
builder.Services.AddDbContext<ChatDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Default");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'Default' is not configured.");
    }

    options.UseSqlServer(connectionString);
});

// ------------------------------------------------------------------- identity
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        // Accounts are never confirmed by email: there is no mail sender in this
        // build, and password resets are performed by an administrator instead.
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;

        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ChatDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        bearer.MapInboundClaims = false;

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = JwtClaimNames.Email,

            // Roles are written under the literal "role" claim with inbound mapping
            // switched off, so authorization has to be told which claim carries the
            // role. Without this [Authorize(Roles = "Admin")] silently never matches.
            RoleClaimType = JwtClaimNames.Role
        };

        // A browser cannot set an Authorization header on a WebSocket handshake,
        // so the SignalR client passes its token as a query string parameter. Only
        // the hub path is allowed to authenticate that way.
        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ----------------------------------------------------------- application layer
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IChatBroadcaster, ChatBroadcaster>();

builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IChatMessageService, ChatMessageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<DbInitializer>();

// --------------------------------------------------------------------- the bot
builder.Services
    .AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.SectionName));

builder.Services
    .AddOptions<AiChatClientOptions>()
    .Bind(builder.Configuration.GetSection(AiChatClientOptions.SectionName));

builder.Services.AddSingleton<RuleBasedBotResponder>();
builder.Services.AddHttpClient<OpenAiChatClient>();
builder.Services.AddHttpClient<AnthropicChatClient>();

builder.Services.AddScoped<IAiChatClient>(serviceProvider =>
{
    var provider = serviceProvider.GetRequiredService<IOptions<AiChatClientOptions>>().Value.Provider;

    if (provider == AiProvider.Anthropic)
    {
        return serviceProvider.GetRequiredService<AnthropicChatClient>();
    }

    return serviceProvider.GetRequiredService<OpenAiChatClient>();
});

builder.Services.AddScoped<AiBotResponder>();
builder.Services.AddScoped<FallbackBotResponder>();

// Which responder answers is a deployment decision, so it is settled here in the
// composition root rather than guessed at inside the application layer.
builder.Services.AddScoped<IBotResponder>(serviceProvider =>
{
    var mode = serviceProvider.GetRequiredService<IOptions<BotOptions>>().Value.Mode;

    if (mode == BotMode.Rules)
    {
        return serviceProvider.GetRequiredService<RuleBasedBotResponder>();
    }

    if (mode == BotMode.Ai)
    {
        return serviceProvider.GetRequiredService<AiBotResponder>();
    }

    // Auto: use the AI when it is actually configured, and keep the rules as a safety
    // net so a bad key or an unreachable provider still produces an answer.
    var isConfigured = serviceProvider
        .GetRequiredService<IOptions<AiChatClientOptions>>()
        .Value
        .IsConfigured;

    if (isConfigured)
    {
        return serviceProvider.GetRequiredService<FallbackBotResponder>();
    }

    return serviceProvider.GetRequiredService<RuleBasedBotResponder>();
});

// ------------------------------------------------------------------ web layer
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// SignalR keeps its own serializer settings, separate from the controller ones
// above, so the same enum converter has to be registered here too. Otherwise REST
// returns "User"/"Bot" while the hub returns 0/1 for the very same message.
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

// Applying migrations from the app itself is a demo convenience so that
// 'docker compose up' is genuinely one command. A real deployment runs this as an
// explicit step, hence the opt-in flag rather than an environment name check.
if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();

    await initializer.InitializeAsync();
}

app.Run();

public partial class Program;
