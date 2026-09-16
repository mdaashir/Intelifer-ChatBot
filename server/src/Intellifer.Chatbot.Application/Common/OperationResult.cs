namespace Intellifer.Chatbot.Application.Common;

public sealed class OperationResult
{
    private static readonly IReadOnlyList<string> NoErrors = [];

    private OperationResult(bool succeeded, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<string> Errors { get; }

    public static OperationResult Success() => new(true, NoErrors);

    public static OperationResult Failure(params string[] errors) => new(false, errors);

    public static OperationResult Failure(IEnumerable<string> errors) => new(false, errors.ToArray());
}
