using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Services;

namespace Intellifer.Chatbot.UnitTests.Services;

public sealed class RuleBasedBotResponderTests
{
    private readonly RuleBasedBotResponder _responder = new();

    [Theory]
    [InlineData("hello")]
    [InlineData("Hi")]
    [InlineData("hey there")]
    [InlineData("HELLO!")]
    [InlineData("hello!!!")]
    public async Task RepliesToAGreeting(string content)
    {
        var reply = await Reply(content);

        Assert.Contains("Hello", reply);
    }

    [Theory]
    [InlineData("how are you")]
    [InlineData("Hey, how are you doing?")]
    public async Task RepliesToHowAreYou(string content)
    {
        var reply = await Reply(content);

        Assert.Contains("running fine", reply);
    }

    [Theory]
    [InlineData("who are you")]
    [InlineData("what is your name?")]
    public async Task RepliesToQuestionsAboutItself(string content)
    {
        var reply = await Reply(content);

        Assert.Contains("Intellifer assistant", reply);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("what can you do?")]
    public async Task RepliesToHelpRequests(string content)
    {
        var reply = await Reply(content);

        Assert.Contains("Try", reply);
    }

    [Theory]
    [InlineData("thanks")]
    [InlineData("Thank you!")]
    public async Task RepliesToThanks(string content)
    {
        var reply = await Reply(content);

        Assert.Equal("You're welcome!", reply);
    }

    [Theory]
    [InlineData("bye")]
    [InlineData("goodbye for now")]
    public async Task RepliesToFarewells(string content)
    {
        var reply = await Reply(content);

        Assert.Contains("Goodbye", reply);
    }

    [Theory]
    [InlineData("how do I configure a Kubernetes ingress")]
    [InlineData("12345")]
    [InlineData("blah blah blah")]
    public async Task FallsBackWhenNothingMatches(string content)
    {
        var fallback = await Reply("zzzz qqqq");

        Assert.Equal(fallback, await Reply(content));
    }

    [Fact]
    public async Task DoesNotTreatAWordThatMerelyContainsAGreetingAsAGreeting()
    {
        var fallback = await Reply("zzzz qqqq");

        // "hilarious" contains "hi", "later" is a farewell - neither should be
        // matched loosely enough to answer a different question.
        Assert.Equal(fallback, await Reply("that was hilarious"));
    }

    [Fact]
    public async Task PrefersTheMoreSpecificPhraseOverASingleWordInTheSameMessage()
    {
        var reply = await Reply("hi, how are you?");

        Assert.Contains("running fine", reply);
    }

    [Fact]
    public async Task AnswersFromThePromptWithoutBeingInfluencedByHistory()
    {
        var conversation = new BotConversation(
            "thanks",
            [new BotTurn(Domain.Enums.MessageSender.Bot, "Goodbye!")]);

        var reply = await _responder.GetReplyAsync(conversation);

        Assert.Equal("You're welcome!", reply);
    }

    private Task<string> Reply(string content) =>
        _responder.GetReplyAsync(new BotConversation(content, []));
}
