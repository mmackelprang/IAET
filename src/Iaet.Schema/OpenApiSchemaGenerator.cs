using System.Globalization;
using System.Text;

namespace Iaet.Schema;

/// <summary>
/// Generates an OpenAPI 3.1 schema fragment in YAML format from a <see cref="JsonTypeMap"/>.
/// </summary>
public static class OpenApiSchemaGenerator
{
    /// <summary>
    /// Produces a YAML string representing an OpenAPI schema fragment.
    /// </summary>
    public static string Generate(JsonTypeMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var sb = new StringBuilder();
        sb.AppendLine("type: object");
        sb.AppendLine("properties:");

        foreach (var (name, field) in map.Fields)
        {
            WriteField(sb, name, field, 1);
        }

        return sb.ToString();
    }

    private static void WriteField(StringBuilder sb, string name, FieldInfo field, int indent)
    {
        var prefix = new string(' ', indent * 2);
        sb.AppendLine(CultureInfo.InvariantCulture, $"{prefix}{name}:");

        var childPrefix = new string(' ', (indent + 1) * 2);
        var typeName = MapTypeName(field.JsonType);
        sb.AppendLine(CultureInfo.InvariantCulture, $"{childPrefix}type: {typeName}");

        if (field.IsNullable && field.JsonType != JsonFieldType.Null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{childPrefix}nullable: true");
        }

        if (field.JsonType == JsonFieldType.Object && field.NestedFields is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{childPrefix}properties:");
            foreach (var (childName, childField) in field.NestedFields)
            {
                WriteField(sb, childName, childField, indent + 2);
            }
        }

        if (field.JsonType == JsonFieldType.Array && field.ArrayItemType is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{childPrefix}items:");
            var itemPrefix = new string(' ', (indent + 2) * 2);
            var itemTypeName = MapTypeName(field.ArrayItemType.JsonType);
            sb.AppendLine(CultureInfo.InvariantCulture, $"{itemPrefix}type: {itemTypeName}");

            if (field.ArrayItemType.JsonType == JsonFieldType.Object && field.ArrayItemType.NestedFields is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{itemPrefix}properties:");
                foreach (var (childName, childField) in field.ArrayItemType.NestedFields)
                {
                    WriteField(sb, childName, childField, indent + 3);
                }
            }
        }
    }

    private static string MapTypeName(JsonFieldType fieldType)
    {
        return fieldType switch
        {
            JsonFieldType.String => "string",
            JsonFieldType.Number => "number",
            JsonFieldType.Boolean => "boolean",
            JsonFieldType.Null => "null",
            JsonFieldType.Object => "object",
            JsonFieldType.Array => "array",
            _ => "string",
        };
    }
}
