using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Domain.Enums;
using Intellifer.Chatbot.Infrastructure.Bot;
using Intellifer.Chatbot.UnitTests.Fakes;
using Microsoft.Extensions.Options;

namespace Intellifer.Chatbot.UnitTests.Bot;

public sealed class AiBotResponderTests
{
    private readonly FakeAiChatClient _aiChatClient = new();

    [Fact]
    public async Task SendsTheSystemPromptThenTheHistoryThenThePrompt()
    {
        var responder = CreateResponder(options => options.SystemPrompt = "Be terse.");

        await responder.GetReplyAsync(new BotConversation(
            "how are you",
            [
                new BotTurn(MessageSender.User, "hello"),
                new BotTurn(MessageSender.Bot, "hi there")
            ]));

        var messages = Assert.Single(_aiChatClient.Requests).Messages;

        Assert.Equal(4, messages.Count);
        Assert.Equal(AiChatRole.System, messages[0].Role);
        Assert.Equal("Be terse.", messages[0].Content);

        Assert.Equal(AiChatRole.User, messages[1].Role);
        Assert.Equal("hello", messages[1].Content);

        Assert.Equal(AiChatRole.Assistant, messages[2].Role);
        Assert.Equal("hi there", messages[2].Content);

        Assert.Equal(AiChatRole.User, messages[3].Role);
        Assert.Equal("how are you", messages[3].Content);
    }

    [Fact]
    public async Task MapsBotTurnsToAssistantAndVisitorTurnsToUser()
    {
        var responder = CreateResponder();

        await responder.GetReplyAsync(new BotConversation(
            "third",
            [
                new BotTurn(MessageSender.User, "first"),
                new BotTurn(MessageSender.Bot, "second")
            ]));

        var roles = Assert.Single(_aiChatClient.Requests).Messages.Select(message => message.Role);

        Assert.Equal(
            new[] { AiChatRole.System, AiChatRole.User, AiChatRole.Assistant, AiChatRole.User },
            roles);
    }

    [Fact]
    public async Task KeepsOnlyTheMostRecentTurnsWhenTheHistoryIsLongerThanTheLimit()
    {
        var responder = CreateResponder(options => options.HistoryMessages = 2);

        await responder.GetReplyAsync(new BotConversation(
            "prompt",
            [
                new BotTurn(MessageSender.User, "oldest"),
                new BotTurn(MessageSender.Bot, "older"),
                new BotTurn(MessageSender.User, "newer"),
                new BotTurn(MessageSender.Bot, "newest")
            ]));

        var messages = Assert.Single(_aiChatClient.Requests).Messages;

        // The system prompt comes first, then only the two most recent turns.
        Assert.Equal(
            new[] { "newer", "newest", "prompt" },
            messages.Skip(1).Select(message => message.Content));
    }

    [Fact]
    public async Task SendsNoHistoryWhenTheLimitIsZero()
    {
        var responder = CreateResponder(options => options.HistoryMessages = 0);

        await responder.GetReplyAsync(new BotConversation(
            "prompt",
            [new BotTurn(MessageSender.User, "earlier")]));

        var messages = Assert.Single(_aiChatClient.Requests).Messages;

        Assert.Equal(2, messages.Count);
        Assert.Equal("prompt", messages[^1].Content);
    }

    [Fact]
    public async Task MergesConsecutiveTurnsFromTheSameRole()
    {
        var responder = CreateResponder();

        // Two user turns in a row happen when a bot reply previously failed to persist,
        // and some providers reject a conversation that does not alternate.
        await responder.GetReplyAsync(new BotConversation(
            "and again",
            [
                new BotTurn(MessageSender.User, "first"),
                new BotTurn(MessageSender.User, "second")
            ]));

        var messages = Assert.Single(_aiChatClient.Requests).Messages;

        Assert.Equal(3, messages.Count);
        Assert.Equal(AiChatRole.User, messages[1].Role);
        Assert.Equal("first\nsecond", messages[1].Content);
    }

    [Fact]
    public async Task ReturnsWhateverTheClientReplied()
    {
        _aiChatClient.Reply = "a model answer";

        var responder = CreateResponder();

        var reply = await responder.GetReplyAsync(new BotConversation("hello", []));

        Assert.Equal("a model answer", reply);
    }

    private AiBotResponder CreateResponder(Action<BotOptions>? configure = null)
    {
        var options = new BotOptions();

        configure?.Invoke(options);

        return new AiBotResponder(_aiChatClient, Options.Create(options));
    }
}
