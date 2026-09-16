namespace Intellifer.Chatbot.Web.Common;

public sealed record ApiError(IReadOnlyList<string> Errors)
{
    public static ApiError From(IEnumerable<string> errors) => new(errors.ToList());

    public static ApiError From(string error) => new([error]);
}
