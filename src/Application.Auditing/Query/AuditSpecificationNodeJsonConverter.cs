// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Reads the shared leaf/group wire grammar with a fixed discriminator and JSON-only arguments.</summary>
/// <example><code>options.Converters.Add(new AuditSpecificationNodeJsonConverter());</code></example>
public sealed class AuditSpecificationNodeJsonConverter : JsonConverter<SpecificationNode>
{
    /// <summary>Accepts only fixed leaf/group node shapes without CLR type resolution.</summary>
    /// <example><code>var node = JsonSerializer.Deserialize&lt;SpecificationNode&gt;(json, options);</code></example>
    public override SpecificationNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("AuditQueryInvalid");
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value)) throw new JsonException("AuditQueryInvalid");
        }

        var leaf = properties.ContainsKey("Name");
        var known = leaf ? new[] { "Type", "Name", "Arguments" } : ["Type", "Logic", "Nodes"];
        if (properties.Keys.Any(key => !known.Contains(key, StringComparer.OrdinalIgnoreCase))) throw new JsonException("AuditQueryInvalid");
        if (properties.TryGetValue("Type", out var discriminator) && discriminator.GetString() != (leaf ? nameof(SpecificationLeaf) : nameof(SpecificationGroup))) throw new JsonException("AuditQueryInvalid");
        if (leaf)
        {
            return new SpecificationLeaf
            {
                Name = properties["Name"].GetString(),
                Arguments = properties.TryGetValue("Arguments", out var arguments) ? arguments.Deserialize<object[]>(options) : null
            };
        }

        if (!properties.TryGetValue("Logic", out var logic) || !properties.TryGetValue("Nodes", out var nodes)) throw new JsonException("AuditQueryInvalid");
        return new SpecificationGroup { Logic = logic.Deserialize<FilterLogicOperator>(options), Nodes = nodes.Deserialize<List<SpecificationNode>>(options) };
    }

    /// <summary>Writes the existing fixed leaf/group discriminator grammar.</summary>
    /// <example><code>var json = JsonSerializer.Serialize(node, options);</code></example>
    public override void Write(Utf8JsonWriter writer, SpecificationNode value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value is SpecificationLeaf leaf)
        {
            writer.WriteString("Type", nameof(SpecificationLeaf));
            writer.WriteString("Name", leaf.Name);
            writer.WritePropertyName("Arguments");
            JsonSerializer.Serialize(writer, leaf.Arguments, options);
        }
        else if (value is SpecificationGroup group)
        {
            writer.WriteString("Type", nameof(SpecificationGroup));
            writer.WritePropertyName("Logic");
            JsonSerializer.Serialize(writer, group.Logic, options);
            writer.WritePropertyName("Nodes");
            JsonSerializer.Serialize(writer, group.Nodes, options);
        }
        else throw new JsonException("AuditQueryInvalid");

        writer.WriteEndObject();
    }
}
