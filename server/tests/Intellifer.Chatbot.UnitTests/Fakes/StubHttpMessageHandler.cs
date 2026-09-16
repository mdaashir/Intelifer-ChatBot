namespace Intellifer.Chatbot.UnitTests.Fakes;

/// <summary>
/// Captures what was actually sent so a test can assert on the wire format. The body
/// and headers are read eagerly because the caller disposes the request as soon as it
/// has been sent.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage> _responseFactory;

    public StubHttpMessageHandler(Func<HttpResponseMessage> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public List<CapturedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }
        }

        Requests.Add(new CapturedRequest(
            request.Method.Method,
            request.RequestUri,
            request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken),
            headers));

        return _responseFactory();
    }

    public sealed record CapturedRequest(
        string Method,
        Uri? Uri,
        string Body,
        IReadOnlyDictionary<string, string> Headers);
}
