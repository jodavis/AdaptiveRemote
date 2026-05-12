using System.Collections.Concurrent;

namespace AdaptiveRemote.TestUtilities;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responders;
    private readonly ConcurrentQueue<CapturedHttpRequest> _capturedRequests = new();
    private int _callCount;

    public int CallCount => _callCount;
    public IReadOnlyCollection<CapturedHttpRequest> Requests => _capturedRequests.ToArray();

    public FakeHttpMessageHandler(params HttpResponseMessage[] responses)
        : this(
            responses.Select(
                static response => new Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>(
                    (_, _) => Task.FromResult(response)))
            .ToArray())
    {
    }

    public FakeHttpMessageHandler(params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] responders)
    {
        _responders = new ConcurrentQueue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(responders);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int callNumber = Interlocked.Increment(ref _callCount);

        string? content = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        _capturedRequests.Enqueue(new CapturedHttpRequest(
            request.Method.Method,
            request.RequestUri?.ToString() ?? string.Empty,
            content));

        if (!_responders.TryDequeue(out Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? responder))
        {
            throw new InvalidOperationException(
                $"FakeHttpMessageHandler: no response configured for call #{callNumber}");
        }

        return await responder(request, cancellationToken);
    }
}

public sealed record CapturedHttpRequest(string Method, string Url, string? Body);
