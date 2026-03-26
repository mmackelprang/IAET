// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.Globalization;

namespace Iaet.Cli.Commands;

/// <summary>
/// Reusable interactive-prompt helpers for the investigation wizard.
/// All I/O goes through the <see cref="TextReader"/> / <see cref="TextWriter"/> parameters
/// so the methods can be exercised in unit tests without touching the real console.
/// </summary>
internal static class WizardPrompt
{
    // ── Banner ─────────────────────────────────────────────────────────────

    /// <summary>Prints the IAET investigation wizard welcome banner.</summary>
    internal static void PrintBanner(TextWriter? writer = null)
    {
        var w = writer ?? Console.Out;
        w.WriteLine();
        w.WriteLine("╔══════════════════════════════════════════════════════╗");
        w.WriteLine("║       IAET — Investigation Wizard (Phase 8)          ║");
        w.WriteLine("╚══════════════════════════════════════════════════════╝");
        w.WriteLine();
        w.WriteLine("Welcome to IAET! Let's investigate a web application.");
        w.WriteLine();
    }

    // ── Text input ─────────────────────────────────────────────────────────

    /// <summary>
    /// Prompts the user for a non-empty string.
    /// Loops until a non-whitespace value is entered.
    /// </summary>
    /// <param name="prompt">The question to display (no trailing space/colon required).</param>
    /// <param name="defaultValue">
    ///     Pre-filled value shown in brackets. If the user presses Enter without typing,
    ///     this value is returned. Pass <see langword="null"/> to require explicit input.
    /// </param>
    /// <param name="reader">Optional reader (defaults to <see cref="Console.In"/>).</param>
    /// <param name="writer">Optional writer (defaults to <see cref="Console.Out"/>).</param>
    /// <returns>The value entered by the user, or <paramref name="defaultValue"/>.</returns>
    internal static string ReadString(
        string prompt,
        string? defaultValue = null,
        TextReader? reader = null,
        TextWriter? writer = null)
    {
        var r = reader ?? Console.In;
        var w = writer ?? Console.Out;

        while (true)
        {
            if (defaultValue is not null)
                w.Write($"? {prompt} [{defaultValue}]: ");
            else
                w.Write($"? {prompt}: ");

            var input = r.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                if (defaultValue is not null)
                    return defaultValue;

                w.WriteLine("  (value is required)");
                continue;
            }

            return input.Trim();
        }
    }

    // ── Numbered menu ──────────────────────────────────────────────────────

    /// <summary>
    /// Prints a numbered menu and reads the user's selection.
    /// Returns the zero-based index of the chosen option.
    /// </summary>
    /// <param name="prompt">The question header, e.g. "What next?".</param>
    /// <param name="options">The list of option labels to display.</param>
    /// <param name="reader">Optional reader.</param>
    /// <param name="writer">Optional writer.</param>
    /// <returns>Zero-based index of the chosen option.</returns>
    internal static int ReadChoice(
        string prompt,
        IReadOnlyList<string> options,
        TextReader? reader = null,
        TextWriter? writer = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
            throw new ArgumentException("At least one option must be provided.", nameof(options));

        var r = reader ?? Console.In;
        var w = writer ?? Console.Out;

        while (true)
        {
            w.WriteLine($"? {prompt}");
            for (var i = 0; i < options.Count; i++)
            {
                w.WriteLine($"  {(i + 1).ToString(CultureInfo.InvariantCulture)}: {options[i]}");
            }
            w.Write($"  Enter 1-{options.Count.ToString(CultureInfo.InvariantCulture)}: ");

            var input = r.ReadLine();

            if (int.TryParse(input?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var choice)
                && choice >= 1 && choice <= options.Count)
            {
                return choice - 1; // return zero-based index
            }

            w.WriteLine($"  Invalid selection. Please enter a number between 1 and {options.Count.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    // ── Session name generator ─────────────────────────────────────────────

    /// <summary>
    /// Generates a slug-style session name from a target application name and timestamp.
    /// E.g. "Spotify Web Player" at 2026-03-26T14:05 → "spotify-web-player-20260326-1405"
    /// </summary>
    internal static string GenerateSessionName(string targetName, DateTimeOffset? at = null)
    {
        var ts = at ?? DateTimeOffset.UtcNow;
        var slug = SlugifyTarget(targetName);
        return $"{slug}-{ts:yyyyMMdd}-{ts:HHmm}";
    }

    // ── Table helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Prints a divider line of the requested width.
    /// </summary>
    internal static void PrintDivider(int width = 80, TextWriter? writer = null)
    {
        (writer ?? Console.Out).WriteLine(new string('─', width));
    }

    // ── Internal helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Converts a display name to a lowercase hyphen-separated slug.
    /// Only ASCII letters, digits, and hyphens are kept.
    /// </summary>
    internal static string SlugifyTarget(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "session";

        var chars = new List<char>(name.Length);
        var prevHyphen = false;

        foreach (var c in name)
        {
            // Convert to ASCII lowercase manually to avoid CA1308 (ToLowerInvariant)
            // We only care about ASCII letters: 'A'-'Z' → 'a'-'z'
            var lower = c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;

            if (char.IsAsciiLetterOrDigit(lower))
            {
                chars.Add(lower);
                prevHyphen = false;
            }
            else if (!prevHyphen && chars.Count > 0)
            {
                chars.Add('-');
                prevHyphen = true;
            }
        }

        // Remove trailing hyphen
        while (chars.Count > 0 && chars[^1] == '-')
            chars.RemoveAt(chars.Count - 1);

        return chars.Count == 0 ? "session" : new string([.. chars]);
    }
}
