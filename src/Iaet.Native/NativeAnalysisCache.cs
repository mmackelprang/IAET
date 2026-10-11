using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Iaet.Native;

/// <summary>
/// Content-addressed snapshot cache for expensive provider runs (Ghidra headless imports take
/// minutes). A result is reused only when the target bytes, operation, parameters and provider
/// version all match exactly, so a rebuilt binary or a new script version never serves stale data.
/// </summary>
public sealed class NativeAnalysisCache(string cacheDirectory)
{
    public string CacheDirectory { get; } = cacheDirectory;

    public static string ComputeKey(string artifactSha256, string operation, string providerVersion, string parameters)
    {
        ArgumentNullException.ThrowIfNull(artifactSha256);
        var material = string.Join('\n', artifactSha256, operation, providerVersion, parameters);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexStringLower(hash);
    }

    public string PathFor(string key) => Path.Combine(CacheDirectory, key + ".json");

    public bool TryGet(string key, out string json)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            json = File.ReadAllText(path);
            return true;
        }
        json = string.Empty;
        return false;
    }

    public async Task PutAsync(string key, string json, CancellationToken ct = default)
    {
        Directory.CreateDirectory(CacheDirectory);
        await File.WriteAllTextAsync(PathFor(key), json, ct).ConfigureAwait(false);
    }

    public async Task PutAsync<T>(string key, T value, JsonSerializerOptions options, CancellationToken ct = default)
    {
        await PutAsync(key, JsonSerializer.Serialize(value, options), ct).ConfigureAwait(false);
    }

    public static async Task<string> HashFileAsync(string path, CancellationToken ct = default)
    {
        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexStringLower(hash);
        }
    }
}
