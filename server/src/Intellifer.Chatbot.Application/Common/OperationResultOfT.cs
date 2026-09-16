namespace Intellifer.Chatbot.Application.Common;

public sealed class OperationResult<T>
{
    private static readonly IReadOnlyList<string> NoErrors = [];

    private OperationResult(bool succeeded, T? value, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Value = value;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public IReadOnlyList<string> Errors { get; }

    public static OperationResult<T> Success(T value) => new(true, value, NoErrors);

    public static OperationResult<T> Failure(params string[] errors) => new(false, default, errors);

    public static OperationResult<T> Failure(IEnumerable<string> errors) => new(false, default, errors.ToArray());
}
