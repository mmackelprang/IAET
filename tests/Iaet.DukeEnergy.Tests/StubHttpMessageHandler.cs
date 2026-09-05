using System.Net;

namespace Iaet.DukeEnergy.Tests;

/// <summary>
/// Serves canned responses keyed by a substring of the request URI, and records every request it
/// handled so tests can assert on headers and bodies.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string UriFragment, HttpStatusCode Status, string Body)> _responses = [];

    internal List<HttpRequestMessage> Requests { get; } = [];

    internal List<string> RequestBodies { get; } = [];

    internal StubHttpMessageHandler Respond(string uriFragment, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Add((uriFragment, status, body));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        var url = request.RequestUri?.ToString() ?? string.Empty;

        foreach (var (fragment, status, body) in _responses)
        {
            if (url.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                };
            }
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
