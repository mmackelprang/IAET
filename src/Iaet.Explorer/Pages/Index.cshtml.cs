using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Iaet.Explorer.Pages;

/// <summary>Page model for the sessions list.</summary>
public sealed class IndexModel : PageModel
{
    private readonly IEndpointCatalog _catalog;

    /// <summary>Gets the list of capture sessions.</summary>
    public IReadOnlyList<CaptureSessionInfo> Sessions { get; private set; } = [];

    /// <summary>Initializes a new instance of <see cref="IndexModel"/>.</summary>
    public IndexModel(IEndpointCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Handles GET requests.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Sessions = await _catalog.ListSessionsAsync(ct).ConfigureAwait(false);
    }
}
