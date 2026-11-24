// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Reads and writes bounded blob filters with explicitly typed operands.</summary>
/// <example><code>var json = JsonSerializer.Serialize(filter);</code></example>
public sealed class BlobDimensionFilterJsonConverter : JsonConverter<BlobDimensionFilter>
{
    /// <inheritdoc />
    public override BlobDimensionFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var predicates = 0;
        var groups = 0;
        try { return Visit(document.RootElement, 0); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException) { throw Invalid(); }

        BlobDimensionFilter Visit(JsonElement node, int depth)
        {
            if (node.ValueKind != JsonValueKind.Object) throw Invalid();
            var properties = node.EnumerateObject().ToArray();
            var all = node.TryGetProperty("all", out var children);
            var any = node.TryGetProperty("any", out var alternatives);
            if (all || any)
            {
                if (properties.Length != 1 || all && any || ++groups > BlobDimensionLimits.MaximumGroups || depth + 1 > BlobDimensionLimits.MaximumGroupDepth) throw Invalid();
                var array = all ? children : alternatives;
                if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0) throw Invalid();
                var nested = array.EnumerateArray().Select(child => Visit(child, depth + 1)).ToArray();
                return all ? BlobDimensionFilter.All(nested) : BlobDimensionFilter.Any(nested);
            }

            if (++predicates > BlobDimensionLimits.MaximumPredicates || !node.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                || !node.TryGetProperty("operator", out var operation) || operation.ValueKind != JsonValueKind.String
                || !Enum.GetNames<BlobDimensionOperator>().Any(value => string.Equals(value, operation.GetString(), StringComparison.OrdinalIgnoreCase))
                || !Enum.TryParse<BlobDimensionOperator>(operation.GetString(), true, out var parsed)) throw Invalid();
            var hasOperand = node.TryGetProperty("operand", out var envelope);
            if (properties.Length != (hasOperand ? 3 : 2) || parsed >= BlobDimensionOperator.Exists && hasOperand || parsed < BlobDimensionOperator.Exists && !hasOperand) throw Invalid();
            object scalar = null;
            if (hasOperand)
            {
                var collection = JsonSerializer.Deserialize<BlobDimensions>("{\"operand\":" + envelope.GetRawText() + "}", options);
                if (collection is null || collection.Count != 1) throw Invalid();
                scalar = collection["operand"].Value;
            }

            var result = BlobDimensionPredicate.Create(name.GetString(), parsed, scalar);
            return result.IsSuccess ? BlobDimensionFilter.Where(result.Value) : throw Invalid();
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, BlobDimensionFilter value, JsonSerializerOptions options)
    {
        var validation = BlobDimensionFilterValidator.Validate(value, new() { SupportsQueries = true, SupportsDecimalComparisons = true });
        if (validation.IsFailure) throw Invalid();
        WriteNode(value);

        void WriteNode(BlobDimensionFilter node)
        {
            writer.WriteStartObject();
            if (node.Predicate is { } predicate)
            {
                writer.WriteString("name", predicate.Name);
                writer.WriteString("operator", predicate.Operator.ToString());
                if (predicate.Operand is { } operand)
                {
                    writer.WritePropertyName("operand");
                    var bag = BlobDimensions.Create(new Dictionary<string, object> { ["operand"] = operand.Value }).Value;
                    using var document = JsonDocument.Parse(JsonSerializer.Serialize(bag, options));
                    document.RootElement.GetProperty("operand").WriteTo(writer);
                }
            }
            else
            {
                writer.WritePropertyName(node.IsAny ? "any" : "all");
                writer.WriteStartArray();
                foreach (var child in node.Children) WriteNode(child);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
    }

    private static JsonException Invalid() => new("Dimension filters require bounded all/any groups or a named predicate with a supported operator and typed operand.");
}
