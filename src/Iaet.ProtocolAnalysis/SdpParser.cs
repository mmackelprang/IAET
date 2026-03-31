namespace Iaet.ProtocolAnalysis;

public static class SdpParser
{
    public static SdpResult Parse(string sdp)
    {
        if (string.IsNullOrWhiteSpace(sdp))
            return new SdpResult();

        var lines = sdp.Split('\n', StringSplitOptions.TrimEntries);
        var mediaSections = new List<SdpMediaSection>();
        SdpMediaSection? currentMedia = null;
        string? iceUfrag = null, icePwd = null, fingerprint = null, bundleGroup = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("m=", StringComparison.Ordinal))
            {
                currentMedia = new SdpMediaSection { Type = line[2..].Split(' ')[0] };
                mediaSections.Add(currentMedia);
            }
            else if (line.StartsWith("a=rtpmap:", StringComparison.Ordinal))
            {
                var parts = line["a=rtpmap:".Length..].Split(' ', 2);
                if (parts.Length == 2 && currentMedia is not null)
                    currentMedia.CodecList.Add(parts[1]);
            }
            else if (line.StartsWith("a=ice-ufrag:", StringComparison.Ordinal))
            {
                iceUfrag = line["a=ice-ufrag:".Length..];
            }
            else if (line.StartsWith("a=ice-pwd:", StringComparison.Ordinal))
            {
                icePwd = line["a=ice-pwd:".Length..];
            }
            else if (line.StartsWith("a=fingerprint:", StringComparison.Ordinal))
            {
                fingerprint = line["a=fingerprint:".Length..];
            }
            else if (line.StartsWith("a=group:BUNDLE", StringComparison.Ordinal))
            {
                bundleGroup = line["a=group:BUNDLE ".Length..];
            }
        }

        return new SdpResult
        {
            MediaSections = mediaSections,
            IceUfrag = iceUfrag,
            IcePwd = icePwd,
            Fingerprint = fingerprint,
            BundleGroup = bundleGroup,
        };
    }
}

public sealed class SdpMediaSection
{
    public required string Type { get; init; }
    internal List<string> CodecList { get; } = [];
    public IReadOnlyList<string> Codecs => CodecList;
}

public sealed record SdpResult
{
    public IReadOnlyList<SdpMediaSection> MediaSections { get; init; } = [];
    public string? IceUfrag { get; init; }
    public string? IcePwd { get; init; }
    public string? Fingerprint { get; init; }
    public string? BundleGroup { get; init; }
}
