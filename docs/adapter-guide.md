# Writing a Custom API Adapter

## What Are Adapters?

`IApiAdapter` is the extension point that lets you attach **target-specific knowledge**
to the generic IAET capture pipeline. When IAET normalizes a raw `CapturedRequest`
into an `EndpointGroup`, it calls every registered adapter in turn. The first adapter
whose `CanHandle` returns `true` gets to enrich the endpoint with an `EndpointDescriptor`
that carries a human-readable operation name, parameter metadata, and authentication type.

Without an adapter, every endpoint is stored with its raw HTTP method and normalized
path. With an adapter, `GET /v1/tracks/{id}` can be recorded as
`"Get Track" (authenticated, path-param: trackId)`.

---

## When to Write One

Write an adapter when:

- You are repeatedly investigating the same target application and want richer labels.
- You know the authentication scheme (e.g. OAuth bearer, cookie session, API key).
- You want to tag certain paths as read-only vs. mutating for downstream tooling.
- You are building a team playbook and want consistent endpoint naming.

The generic pipeline works fine without adapters — write one only when the extra
context is worth the effort.

---

## Interface

```csharp
// Iaet.Core.Abstractions.IApiAdapter
public interface IApiAdapter
{
    /// <summary>Returns true if this adapter can enrich the given request.</summary>
    bool CanHandle(CapturedRequest request);

    /// <summary>Returns an enriched descriptor for the request.</summary>
    EndpointDescriptor Describe(CapturedRequest request);
}
```

`CanHandle` is called first and must be cheap (URL string comparison is fine).
`Describe` is called only when `CanHandle` returned `true`.

---

## Step-by-Step: Creating a SpotifyAdapter

### 1. Create a Class Library

```bash
dotnet new classlib -n Iaet.Adapters.Spotify -o src/Iaet.Adapters.Spotify
dotnet add src/Iaet.Adapters.Spotify reference src/Iaet.Core
```

### 2. Implement `IApiAdapter`

```csharp
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Adapters.Spotify;

public sealed class SpotifyAdapter : IApiAdapter
{
    private const string SpotifyApiHost = "api.spotify.com";

    public bool CanHandle(CapturedRequest request)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Equals(SpotifyApiHost, StringComparison.OrdinalIgnoreCase);
    }

    public EndpointDescriptor Describe(CapturedRequest request)
    {
        var uri       = new Uri(request.Url);
        var segments  = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var operation = BuildOperationName(request.HttpMethod, segments);

        return new EndpointDescriptor(
            OperationName: operation,
            AuthType: DetectAuthType(request),
            Tags: BuildTags(segments));
    }

    // ------------------------------------------------------------------

    private static string BuildOperationName(string method, string[] segments)
    {
        // E.g. GET /v1/me/player → "Get Current Player State"
        // Fall back to "METHOD /path" for unrecognised paths.
        return (method, string.Join("/", segments)) switch
        {
            ("GET",  "v1/me")                   => "Get Current User Profile",
            ("GET",  "v1/me/player")             => "Get Current Player State",
            ("PUT",  "v1/me/player/play")        => "Start or Resume Playback",
            ("PUT",  "v1/me/player/pause")       => "Pause Playback",
            ("GET",  "v1/me/playlists")          => "Get Current User Playlists",
            ("GET",  var p) when p.StartsWith("v1/tracks", StringComparison.Ordinal)
                                                 => "Get Track",
            ("GET",  var p) when p.StartsWith("v1/albums", StringComparison.Ordinal)
                                                 => "Get Album",
            ("GET",  var p) when p.StartsWith("v1/artists", StringComparison.Ordinal)
                                                 => "Get Artist",
            ("GET",  var p) when p.StartsWith("v1/search", StringComparison.Ordinal)
                                                 => "Search",
            _ => $"{method} /{string.Join("/", segments)}",
        };
    }

    private static string DetectAuthType(CapturedRequest request)
    {
        if (request.RequestHeaders.TryGetValue("Authorization", out var auth) &&
            auth.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return "OAuth2-Bearer";
        }

        return "None";
    }

    private static IReadOnlyList<string> BuildTags(string[] segments)
    {
        // Tag by top-level resource type, e.g. "tracks", "albums", "me"
        var tags = new List<string>();
        if (segments.Length >= 2)
            tags.Add(segments[1]);  // "v1" is always index 0
        return tags;
    }
}
```

### 3. Register with DI

In your application's `Program.cs` (or wherever you configure services), register
the adapter **after** `AddIaetCatalog`:

```csharp
services.AddIaetCatalog($"DataSource={dbPath}");
services.AddSingleton<IApiAdapter, SpotifyAdapter>();
```

Multiple adapters can be registered; the first one whose `CanHandle` returns `true`
for a given request wins.

---

## Tips

- Keep `CanHandle` fast — it is called for every request. A single `Host` comparison
  is ideal.
- Adapters are stateless value-processors; keep no mutable state.
- You can use the `EndpointDescriptor.Tags` list to group endpoints in the CLI output
  and exported reports.
- If you want the adapter available across multiple projects, publish it as a NuGet
  package and reference it from your CLI host project.

---

## See Also

- `IApiAdapter` definition: `src/Iaet.Core/Abstractions/IApiAdapter.cs`
- `EndpointDescriptor` model: `src/Iaet.Core/Models/EndpointDescriptor.cs`
- Spotify tutorial: `docs/tutorials/investigating-spotify.md`
