using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Application.Services;
using Intellifer.Chatbot.Infrastructure.Bot;
using Intellifer.Chatbot.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Intellifer.Chatbot.IntegrationTests;

/// <summary>
/// The choice of responder lives in the composition root, so it is only really proven
/// by resolving it out of a real container. The headline claim - that the project runs
/// with no API key at all - rests on the first test here.
/// </summary>
[Collection(ChatbotCollection.Name)]
public sealed class BotResponderSelectionTests
{
    private readonly ChatbotEnvironmentFixture _environment;

    public BotResponderSelectionTests(ChatbotEnvironmentFixture environment)
    {
        _environment = environment;
    }

    [Fact]
    public void DefaultsToTheRulesWhenNoProviderIsConfigured()
    {
        using var factory = CreateFactory(("Bot:Mode", "Auto"));

        Assert.IsType<RuleBasedBotResponder>(Resolve(factory));
    }

    [Fact]
    public void FallsBackToTheRulesWhenTheModeIsAutoAndAProviderIsConfigured()
    {
        using var factory = CreateFactory(
            ("Bot:Mode", "Auto"),
            ("Bot:Ai:BaseUrl", "https://api.example.com/v1"),
            ("Bot:Ai:ApiKey", "test-key"));

        Assert.IsType<FallbackBotResponder>(Resolve(factory));
    }

    [Fact]
    public void UsesTheAiAloneWhenTheModeForcesIt()
    {
        using var factory = CreateFactory(("Bot:Mode", "Ai"));

        Assert.IsType<AiBotResponder>(Resolve(factory));
    }

    [Fact]
    public void NeverTouchesTheAiWhenTheModeIsRulesEvenIfAProviderIsConfigured()
    {
        using var factory = CreateFactory(
            ("Bot:Mode", "Rules"),
            ("Bot:Ai:BaseUrl", "https://api.example.com/v1"),
            ("Bot:Ai:ApiKey", "test-key"));

        Assert.IsType<RuleBasedBotResponder>(Resolve(factory));
    }

    [Fact]
    public void TreatsAHalfConfiguredProviderAsNotConfigured()
    {
        // A base url with no key is a misconfiguration, and must not be treated as
        // "the AI is available" and then fail on every message.
        using var factory = CreateFactory(
            ("Bot:Mode", "Auto"),
            ("Bot:Ai:BaseUrl", "https://api.example.com/v1"),
            ("Bot:Ai:ApiKey", "   "));

        Assert.IsType<RuleBasedBotResponder>(Resolve(factory));
    }

    private ChatbotApiFactory CreateFactory(params (string Key, string Value)[] settings) =>
        new(
            _environment.ConnectionString,
            settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value));

    private static IBotResponder Resolve(ChatbotApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IBotResponder>();
    }
}
