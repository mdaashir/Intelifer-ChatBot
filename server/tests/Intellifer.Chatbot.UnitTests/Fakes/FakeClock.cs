using Intellifer.Chatbot.Application.Common;

namespace Intellifer.Chatbot.UnitTests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}
