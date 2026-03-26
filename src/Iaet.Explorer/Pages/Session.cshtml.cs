using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Iaet.Explorer.Pages;

/// <summary>Page model for the session detail view showing endpoint groups and streams summary.</summary>
public sealed class SessionModel : PageModel
{
    private readonly IEndpointCatalog _catalog;
    private readonly IStreamCatalog _streamCatalog;

    /// <summary>Gets the capture session info.</summary>
    public CaptureSessionInfo? Session { get; private set; }

    /// <summary>Gets the endpoint groups for the session.</summary>
    public IReadOnlyList<EndpointGroup> EndpointGroups { get; private set; } = [];

    /// <summary>Gets the streams for the session.</summary>
    public IReadOnlyList<CapturedStream> Streams { get; private set; } = [];

    /// <summary>Initializes a new instance of <see cref="SessionModel"/>.</summary>
    public SessionModel(IEndpointCatalog catalog, IStreamCatalog streamCatalog)
    {
        _catalog = catalog;
        _streamCatalog = streamCatalog;
    }

    /// <summary>Handles GET requests.</summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var sessions = await _catalog.ListSessionsAsync(ct).ConfigureAwait(false);
        Session = sessions.FirstOrDefault(s => s.Id == id);
        if (Session is null) return NotFound();

        EndpointGroups = await _catalog.GetEndpointGroupsAsync(id, ct).ConfigureAwait(false);
        Streams = await _streamCatalog.GetStreamsBySessionAsync(id, ct).ConfigureAwait(false);
        return Page();
    }
}
