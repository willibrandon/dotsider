using System.Net;

namespace Dotsider.Deploy.Tests;

/// <summary>
/// Returns deterministic HTTP responses for deployment-host command tests.
/// Requested URIs are retained so tests can verify encoded Prometheus queries.
/// A caller-provided response factory controls status and content per request.
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage>? responseFactory = null) : HttpMessageHandler
{
    /// <summary>
    /// Gets the request URIs observed by the handler.
    /// Entries preserve invocation order across all responses.
    /// Only absolute URIs produced by tested commands are recorded.
    /// </summary>
    internal List<Uri> RequestUris { get; } = [];

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestUris.Add(request.RequestUri!);
        HttpResponseMessage response = responseFactory?.Invoke(request)
            ?? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"result\":[{\"value\":[0,\"1.25\"]}]}}"),
            };
        return Task.FromResult(response);
    }
}
