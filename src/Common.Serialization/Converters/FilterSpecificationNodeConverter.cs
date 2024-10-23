// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class FilterSpecificationNodeConverter : JsonConverter<SpecificationNode>
{
    public override SpecificationNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Determine the type of SpecificationNode (abstract) based on the properties
        if (root.TryGetProperty("Name", out _) || root.TryGetProperty("name", out _))
        {
            return JsonSerializer.Deserialize<SpecificationLeaf>(root.GetRawText(), options);
        }
        else if (root.TryGetProperty("Logic", out _) || root.TryGetProperty("logic", out _))
        {
            return JsonSerializer.Deserialize<SpecificationGroup>(root.GetRawText(), options);
        }

        throw new NotSupportedException("Unknown SpecificationNode type.");
    }

    public override void Write(Utf8JsonWriter writer, SpecificationNode value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case SpecificationLeaf leaf:
                writer.WriteStartObject();
                writer.WriteString("Type", nameof(SpecificationLeaf));
                writer.WriteString("Name", leaf.Name);
                writer.WritePropertyName(nameof(SpecificationLeaf.Arguments));
                JsonSerializer.Serialize(writer, leaf.Arguments, options);
                writer.WriteEndObject();
                break;

            case SpecificationGroup group:
                writer.WriteStartObject();
                writer.WriteString("Type", nameof(SpecificationGroup));
                writer.WriteString("Logic", group.Logic.ToString());
                writer.WritePropertyName(nameof(SpecificationGroup.Nodes));
                JsonSerializer.Serialize(writer, group.Nodes, options);
                writer.WriteEndObject();
                break;

            default:
                throw new NotSupportedException($"Type '{value.GetType()}' is not supported.");
        }
    }
}