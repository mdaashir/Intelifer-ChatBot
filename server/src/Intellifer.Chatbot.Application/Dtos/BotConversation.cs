namespace Intellifer.Chatbot.Application.Dtos;

/// <summary>
/// What a responder is asked to answer: the new message, plus the turns that came
/// before it so a responder with memory of the conversation can use them. The
/// rule-based responder ignores the history entirely.
/// </summary>
public sealed record BotConversation(string Prompt, IReadOnlyList<BotTurn> History)
{
    /// <summary>
    /// Upper bound on how much prior conversation is loaded per reply, so a long
    /// running session cannot turn into an unbounded query.
    /// </summary>
    public const int MaxHistoryTurns = 40;
}
