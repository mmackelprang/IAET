using System.Text.Json;

namespace Iaet.Schema;

/// <summary>
/// Generates a JSON Schema (draft-07) string from a <see cref="JsonTypeMap"/>.
/// </summary>
public static class JsonSchemaGenerator
{
    /// <summary>
    /// Produces a JSON Schema draft-07 string from the given type map.
    /// </summary>
    public static string Generate(JsonTypeMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("$schema", "http://json-schema.org/draft-07/schema#");
            writer.WriteString("type", "object");
            WriteProperties(writer, map.Fields);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteProperties(Utf8JsonWriter writer, IReadOnlyDictionary<string, FieldInfo> fields)
    {
        writer.WriteStartObject("properties");

        foreach (var (name, field) in fields)
        {
            writer.WriteStartObject(name);
            WriteFieldSchema(writer, field);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteFieldSchema(Utf8JsonWriter writer, FieldInfo field)
    {
        var typeName = MapTypeName(field.JsonType);

        if (field.IsNullable && field.JsonType != JsonFieldType.Null)
        {
            writer.WriteStartArray("type");
            writer.WriteStringValue(typeName);
            writer.WriteStringValue("null");
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteString("type", typeName);
        }

        if (field.JsonType == JsonFieldType.Object && field.NestedFields is not null)
        {
            WriteProperties(writer, field.NestedFields);
        }

        if (field.JsonType == JsonFieldType.Array && field.ArrayItemType is not null)
        {
            writer.WriteStartObject("items");
            WriteFieldSchema(writer, field.ArrayItemType);
            writer.WriteEndObject();
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
