using Iaet.Core.Models;

namespace Iaet.Catalog;

public static class EndpointNormalizer
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings")]
    public static string Normalize(string method, string fullUrl)
    {
        ArgumentNullException.ThrowIfNull(fullUrl);

        if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out var uri))
        {
            // Handle relative URLs or malformed URIs — use the raw string as the path
            var path = fullUrl.Split('?', 2)[0];
            var sig = EndpointSignature.FromRequest(method, path);
            return sig.Normalized;
        }

        var absSig = EndpointSignature.FromRequest(method, uri.AbsolutePath);
        return absSig.Normalized;
    }
}
