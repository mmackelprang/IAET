using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Iaet.Explorer.Pages;

/// <summary>Page model for the endpoint detail view showing requests and inferred schema.</summary>
public sealed class EndpointModel : PageModel
{
    private readonly IEndpointCatalog _catalog;
    private readonly ISchemaInferrer _schemaInferrer;

    /// <summary>Gets the capture session info.</summary>
    public CaptureSessionInfo? Session { get; private set; }

    /// <summary>Gets the endpoint signature string (URL-decoded).</summary>
    public string Sig { get; private set; } = string.Empty;

    /// <summary>Gets the requests for this endpoint.</summary>
    public IReadOnlyList<CapturedRequest> Requests { get; private set; } = [];

    /// <summary>Gets the inferred schema result, or null if none available.</summary>
    public SchemaResult? Schema { get; private set; }

    /// <summary>Initializes a new instance of <see cref="EndpointModel"/>.</summary>
    public EndpointModel(IEndpointCatalog catalog, ISchemaInferrer schemaInferrer)
    {
        _catalog = catalog;
        _schemaInferrer = schemaInferrer;
    }

    /// <summary>Handles GET requests.</summary>
    public async Task<IActionResult> OnGetAsync(Guid id, string sig, CancellationToken ct)
    {
        var sessions = await _catalog.ListSessionsAsync(ct).ConfigureAwait(false);
        Session = sessions.FirstOrDefault(s => s.Id == id);
        if (Session is null) return NotFound();

        Sig = Uri.UnescapeDataString(sig);

        var allRequests = await _catalog.GetRequestsBySessionAsync(id, ct).ConfigureAwait(false);
        Requests = allRequests
            .Where(r => MatchesSig(r, Sig))
            .OrderByDescending(r => r.Timestamp)
            .ToList();

        var bodies = await _catalog.GetResponseBodiesAsync(id, Sig, ct).ConfigureAwait(false);
        if (bodies.Count > 0)
        {
            Schema = await _schemaInferrer.InferAsync(bodies, ct).ConfigureAwait(false);
        }

        return Page();
    }

    private static bool MatchesSig(CapturedRequest request, string sig)
    {
        var parts = sig.Split(' ', 2);
        if (parts.Length != 2) return false;
        var method = parts[0];
        var path = parts[1];
        return string.Equals(request.HttpMethod, method, StringComparison.OrdinalIgnoreCase)
            && NormalizePath(request.Url).Equals(path, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join("/",
            segments.Select(s => IsId(s) ? "{id}" : s));
        return "/" + normalized;
    }

    private static bool IsId(string segment) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            segment,
            @"^(\d+|[0-9a-f]{8,}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
