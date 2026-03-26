using System.Globalization;
using System.Text;

namespace Iaet.Schema;

/// <summary>
/// Generates a C# record definition from a <see cref="JsonTypeMap"/>.
/// </summary>
public static class CSharpRecordGenerator
{
    /// <summary>
    /// Produces a C# record source string from the given type map.
    /// </summary>
    public static string Generate(JsonTypeMap map, string typeName)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(typeName);

        var sb = new StringBuilder();
        var nestedRecords = new List<string>();
        GenerateRecord(sb, map.Fields, typeName, 0, nestedRecords);

        foreach (var nested in nestedRecords)
        {
            sb.AppendLine();
            sb.Append(nested);
        }

        return sb.ToString();
    }

    private static void GenerateRecord(
        StringBuilder sb,
        IReadOnlyDictionary<string, FieldInfo> fields,
        string typeName,
        int indent,
        List<string> nestedRecords)
    {
        var prefix = new string(' ', indent * 4);
        sb.Append(CultureInfo.InvariantCulture, $"{prefix}public sealed record {typeName}(");

        var first = true;
        foreach (var (name, field) in fields)
        {
            if (!first)
            {
                sb.Append(", ");
            }

            first = false;

            var csharpType = MapCSharpType(field, name, nestedRecords);
            var pascalName = ToPascalCase(name);

            sb.Append(CultureInfo.InvariantCulture, $"{csharpType} {pascalName}");
        }

        sb.AppendLine(");");
    }

    private static string MapCSharpType(FieldInfo field, string fieldName, List<string> nestedRecords)
    {
        var nullable = field.IsNullable ? "?" : "";

        return field.JsonType switch
        {
            JsonFieldType.String => $"string{nullable}",
            JsonFieldType.Number => $"double{nullable}",
            JsonFieldType.Boolean => $"bool{nullable}",
            JsonFieldType.Null => "object?",
            JsonFieldType.Object when field.NestedFields is not null => GenerateNestedType(field, fieldName, nestedRecords, nullable),
            JsonFieldType.Array when field.ArrayItemType is not null => GenerateArrayType(field.ArrayItemType, fieldName, nestedRecords, nullable),
            _ => $"object{nullable}",
        };
    }

    private static string GenerateNestedType(FieldInfo field, string fieldName, List<string> nestedRecords, string nullable)
    {
        var nestedTypeName = ToPascalCase(fieldName) + "Type";
        var nestedSb = new StringBuilder();
        GenerateRecord(nestedSb, field.NestedFields!, nestedTypeName, 0, nestedRecords);
        nestedRecords.Add(nestedSb.ToString());
        return $"{nestedTypeName}{nullable}";
    }

    private static string GenerateArrayType(FieldInfo itemType, string fieldName, List<string> nestedRecords, string nullable)
    {
        var itemCsharpType = itemType.JsonType switch
        {
            JsonFieldType.String => "string",
            JsonFieldType.Number => "double",
            JsonFieldType.Boolean => "bool",
            JsonFieldType.Object when itemType.NestedFields is not null => GenerateNestedTypeForArrayItem(itemType, fieldName, nestedRecords),
            _ => "object",
        };

        return $"IReadOnlyList<{itemCsharpType}>{nullable}";
    }

    private static string GenerateNestedTypeForArrayItem(FieldInfo itemType, string fieldName, List<string> nestedRecords)
    {
        var nestedTypeName = ToPascalCase(fieldName) + "Item";
        var nestedSb = new StringBuilder();
        GenerateRecord(nestedSb, itemType.NestedFields!, nestedTypeName, 0, nestedRecords);
        nestedRecords.Add(nestedSb.ToString());
        return nestedTypeName;
    }

    /// <summary>
    /// Converts a JSON field name to PascalCase.
    /// Handles snake_case and camelCase.
    /// </summary>
    internal static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var sb = new StringBuilder();
        var capitalizeNext = true;

        foreach (var ch in name)
        {
            if (ch == '_' || ch == '-')
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(ch));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}
