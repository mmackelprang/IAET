namespace Iaet.Export;

/// <summary>
/// Shared utility for redacting sensitive HTTP header values before export.
/// </summary>
public static class HeaderRedactor
{
    /// <summary>
    /// Returns <c>&lt;REDACTED&gt;</c> when the header name or value indicates a credential,
    /// or when the value already contains the sentinel. All other values are returned unchanged.
    /// </summary>
    /// <param name="key">The header name (case-insensitive).</param>
    /// <param name="value">The header value.</param>
    /// <returns>The original value, or <c>&lt;REDACTED&gt;</c>.</returns>
    public static string RedactHeaderValue(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (value.Contains("<REDACTED>", StringComparison.OrdinalIgnoreCase))
            return "<REDACTED>";

        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return "<REDACTED>";

        if (value.Contains("session=", StringComparison.OrdinalIgnoreCase))
            return "<REDACTED>";

        if (key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("X-CSRF-Token", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase))
        {
            return "<REDACTED>";
        }

        return value;
    }
}
