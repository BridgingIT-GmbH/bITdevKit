namespace BridgingIT.DevKit.Infrastructure.FileSystem;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Serializes property bags using the storage scalar codec to preserve supported scalar types in manifests.</summary>
/// <example><code>var json = new JsonSerializerOptions(); json.Converters.Add(new FileSystemPropertyBagConverter());</code></example>
public sealed class FileSystemPropertyBagConverter : JsonConverter<PropertyBag>
{
    /// <inheritdoc />
    public override PropertyBag Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options) ?? throw new JsonException("Missing properties.");
        var bag = new PropertyBag();

        foreach (var pair in values)
        {
            bag.Set(pair.Key, PropertyBagScalarCodec.Decode(pair.Value));
        }

        return bag;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, PropertyBag value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var pair in value)
        {
            writer.WriteString(pair.Key, PropertyBagScalarCodec.Encode(pair.Value));
        }

        writer.WriteEndObject();
    }
}