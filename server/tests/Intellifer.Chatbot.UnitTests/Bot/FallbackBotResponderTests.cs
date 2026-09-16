using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Services;
using Intellifer.Chatbot.Infrastructure.Bot;
using Intellifer.Chatbot.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intellifer.Chatbot.UnitTests.Bot;

public sealed class FallbackBotResponderTests
{
    private static readonly BotConversation Conversation = new("hello", []);

    private readonly FakeAiChatClient _aiChatClient = new();
    private readonly RuleBasedBotResponder _ruleBased = new();

    [Fact]
    public async Task AnswersWithTheAiWhenItSucceeds()
    {
        _aiChatClient.Reply = "a model answer";

        var reply = await CreateResponder().GetReplyAsync(Conversation);

        Assert.Equal("a model answer", reply);
    }

    [Fact]
    public async Task AnswersWithTheRulesWhenTheProviderFails()
    {
        _aiChatClient.Throws = new AiChatClientException("OpenAI returned 401");

        var reply = await CreateResponder().GetReplyAsync(Conversation);

        // "hello" is a greeting, so the rule-based reply is the greeting one.
        Assert.Contains("Hello", reply);
    }

    [Fact]
    public async Task GivesTheFallbackTheSameConversationItWasAsked()
    {
        _aiChatClient.Throws = new AiChatClientException("OpenAI returned 429");

        var reply = await CreateResponder().GetReplyAsync(new BotConversation(
            "thanks",
            [new BotTurn(Domain.Enums.MessageSender.Bot, "Hello!")]));

        Assert.Equal("You're welcome!", reply);
    }

    [Fact]
    public async Task DoesNotSwallowUnexpectedExceptions()
    {
        _aiChatClient.Throws = new InvalidOperationException("a bug, not a provider failure");

        // Only the provider's own failure type is caught, so a genuine bug surfaces
        // instead of being hidden behind a canned reply.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateResponder().GetReplyAsync(Conversation));
    }

    private FallbackBotResponder CreateResponder() => new(
        new AiBotResponder(_aiChatClient, Microsoft.Extensions.Options.Options.Create(new BotOptions())),
        _ruleBased,
        NullLogger<FallbackBotResponder>.Instance);
}
