using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Iaet.Export.Generators;

/// <summary>
/// Generates a self-contained HTML report by converting the Markdown output of
/// <see cref="MarkdownReportGenerator"/> to HTML with inline styles.
/// </summary>
public static partial class HtmlReportGenerator
{
    /// <summary>Generates the HTML report string.</summary>
    public static string Generate(ExportContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var markdown = MarkdownReportGenerator.Generate(ctx);
        var body     = ConvertMarkdownToHtml(markdown);

        return WrapInHtml(body);
    }

    // ------------------------------------------------------------------

    private static string WrapInHtml(string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<title>API Investigation Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 1100px; margin: 40px auto; padding: 0 20px; color: #333; }");
        sb.AppendLine("h1 { border-bottom: 2px solid #333; padding-bottom: 8px; }");
        sb.AppendLine("h2 { border-bottom: 1px solid #ccc; padding-bottom: 4px; margin-top: 2em; }");
        sb.AppendLine("h3 { margin-top: 1.5em; }");
        sb.AppendLine("h4 { margin-top: 1em; color: #555; }");
        sb.AppendLine("pre { background: #f4f4f4; border: 1px solid #ddd; border-radius: 4px; padding: 12px; overflow-x: auto; }");
        sb.AppendLine("code { font-family: 'Consolas', 'Courier New', monospace; font-size: 0.9em; }");
        sb.AppendLine("pre code { font-size: 0.85em; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 1em 0; }");
        sb.AppendLine("th, td { border: 1px solid #ccc; padding: 8px 12px; text-align: left; }");
        sb.AppendLine("th { background: #f0f0f0; font-weight: bold; }");
        sb.AppendLine("tr:nth-child(even) { background: #f9f9f9; }");
        sb.AppendLine("blockquote { border-left: 4px solid #ccc; margin: 0; padding-left: 16px; color: #666; }");
        sb.AppendLine("ul { padding-left: 1.5em; }");
        sb.AppendLine("li { margin: 4px 0; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(body);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Converts a subset of Markdown to HTML.
    /// Handles: headings (#), code fences (```), tables (|...|), blockquotes (&gt;),
    /// bold (**), inline code (`), and list items (-).
    /// </summary>
    private static string ConvertMarkdownToHtml(string markdown)
    {
        var lines  = markdown.Split('\n');
        var sb     = new StringBuilder();
        var inCode = false;
        var inTable = false;
        var tableHeaderDone = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // --- code fence ---
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    sb.AppendLine("</code></pre>");
                    inCode = false;
                }
                else
                {
                    sb.AppendLine("<pre><code>");
                    inCode = true;
                }

                continue;
            }

            if (inCode)
            {
                sb.AppendLine(EscapeHtml(line));
                continue;
            }

            // --- close table if we left it ---
            if (inTable && !line.StartsWith('|'))
            {
                sb.AppendLine("</tbody></table>");
                inTable = false;
                tableHeaderDone = false;
            }

            // --- table row ---
            if (line.StartsWith('|'))
            {
                // Skip separator rows (---|---)
                if (SeparatorRowRegex().IsMatch(line))
                    continue;

                var cells = line.Split('|', StringSplitOptions.RemoveEmptyEntries);

                if (!inTable)
                {
                    sb.AppendLine("<table>");
                    sb.AppendLine("<thead><tr>");
                    foreach (var cell in cells)
                        sb.AppendLine(CultureInfo.InvariantCulture, $"<th>{ApplyInlineMarkdown(cell.Trim())}</th>");
                    sb.AppendLine("</tr></thead>");
                    sb.AppendLine("<tbody>");
                    inTable = true;
                    tableHeaderDone = true;
                }
                else
                {
                    _ = tableHeaderDone; // consumed
                    sb.AppendLine("<tr>");
                    foreach (var cell in cells)
                        sb.AppendLine(CultureInfo.InvariantCulture, $"<td>{ApplyInlineMarkdown(cell.Trim())}</td>");
                    sb.AppendLine("</tr>");
                }

                continue;
            }

            // --- headings ---
            if (line.StartsWith("#### ", StringComparison.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"<h4>{ApplyInlineMarkdown(line[5..])}</h4>");
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"<h3>{ApplyInlineMarkdown(line[4..])}</h3>");
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"<h2>{ApplyInlineMarkdown(line[3..])}</h2>");
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"<h1>{ApplyInlineMarkdown(line[2..])}</h1>");
                continue;
            }

            // --- blockquote ---
            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"<blockquote>{ApplyInlineMarkdown(line[2..])}</blockquote>");
                continue;
            }

            // --- list item ---
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"<li>{ApplyInlineMarkdown(line[2..])}</li>");
                continue;
            }

            // --- blank line ---
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine();
                continue;
            }

            // --- paragraph ---
            sb.AppendLine(CultureInfo.InvariantCulture, $"<p>{ApplyInlineMarkdown(line)}</p>");
        }

        // Close any open elements
        if (inCode)
            sb.AppendLine("</code></pre>");

        if (inTable)
            sb.AppendLine("</tbody></table>");

        return sb.ToString();
    }

    private static string ApplyInlineMarkdown(string text)
    {
        // Bold: **text**
        text = BoldRegex().Replace(text, "<strong>$1</strong>");

        // Inline code: `text`
        text = InlineCodeRegex().Replace(text, m =>
            "<code>" + EscapeHtml(m.Groups[1].Value) + "</code>");

        return text;
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"^\|[\s\-:|]+\|[\s\-:|]*$")]
    private static partial Regex SeparatorRowRegex();
}
