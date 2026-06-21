using System.Net;
using System.Text;

namespace TranslatorAzure.Tests.TestSupport;

// Captures outbound requests so Azure client tests assert the HTTP contract without contacting Azure.
internal sealed record CapturedHttpRequest(
    HttpMethod Method,
    Uri? RequestUri,
    string? Body,
    IReadOnlyDictionary<string, string> Headers);

internal sealed class RecordingHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public List<CapturedHttpRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headerPairs = request.Headers;
        if (request.Content is not null)
        {
            headerPairs = headerPairs.Concat(request.Content.Headers);
        }

        var headers = headerPairs
            .ToDictionary(
                pair => pair.Key,
                pair => string.Join(",", pair.Value),
                StringComparer.OrdinalIgnoreCase);

        Requests.Add(new CapturedHttpRequest(request.Method, request.RequestUri, body, headers));
        return await responder(request, cancellationToken);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}

internal sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
