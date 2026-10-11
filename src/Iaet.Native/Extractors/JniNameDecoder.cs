using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Native.Extractors;

/// <summary>
/// Decodes JNI export names (<c>Java_pkg_Class_method__sig</c>) back into Java class and method
/// names, following the JNI mangling rules: <c>_1</c> → <c>_</c>, <c>_2</c> → <c>;</c>, <c>_3</c> → <c>[</c>,
/// <c>_0XXXX</c> → Unicode code unit, <c>__</c> separates an overload signature.
/// </summary>
public static class JniNameDecoder
{
    public static bool IsJniExport(string symbol) =>
        symbol is not null && symbol.StartsWith("Java_", StringComparison.Ordinal) && symbol.Length > 5;

    public static JniBinding? Decode(string symbol)
    {
        if (!IsJniExport(symbol))
            return null;

        var body = symbol[5..];
        string? signature = null;
        var sigIdx = FindSignatureSeparator(body);
        if (sigIdx > 0)
        {
            signature = Demangle(body[(sigIdx + 2)..]);
            body = body[..sigIdx];
        }

        // Split on single underscores that are not escape sequences.
        var parts = SplitUnescaped(body).Select(Demangle).Where(p => p.Length > 0).ToList();
        if (parts.Count < 2)
            return null;

        var method = parts[^1];
        var javaClass = string.Join('.', parts.Take(parts.Count - 1));
        return new JniBinding { Symbol = symbol, JavaClass = javaClass, JavaMethod = method, Signature = signature };
    }

    /// <summary>
    /// Locates the <c>__</c> overload separator. A method name beginning with '_' mangles to <c>_1name</c>, so
    /// <c>Class__1name</c> is separator + escape, not a signature: the second underscore must not start an escape.
    /// </summary>
    private static int FindSignatureSeparator(string s)
    {
        for (var i = 0; i + 1 < s.Length; i++)
        {
            if (s[i] != '_' || s[i + 1] != '_')
                continue;
            var next = i + 2 < s.Length ? s[i + 2] : '\0';
            if (next is not ('0' or '1' or '2' or '3'))
                return i;
        }
        return -1;
    }

    private static List<string> SplitUnescaped(string s)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '_' && i + 1 < s.Length && s[i + 1] is '0' or '1' or '2' or '3')
            {
                // escape sequence — keep for Demangle
                sb.Append(c).Append(s[i + 1]);
                if (s[i + 1] == '0' && i + 5 < s.Length)
                {
                    sb.Append(s, i + 2, 4);
                    i += 4;
                }
                i++;
                continue;
            }
            if (c == '_')
            {
                parts.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        parts.Add(sb.ToString());
        return parts;
    }

    private static string Demangle(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '_' || i + 1 >= s.Length)
            {
                sb.Append(s[i]);
                continue;
            }
            switch (s[i + 1])
            {
                case '1': sb.Append('_'); i++; break;
                case '2': sb.Append(';'); i++; break;
                case '3': sb.Append('['); i++; break;
                case '0' when i + 5 < s.Length && ushort.TryParse(s.AsSpan(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cu):
                    sb.Append((char)cu);
                    i += 5;
                    break;
                default: sb.Append('_'); break;
            }
        }
        return sb.ToString();
    }
}
