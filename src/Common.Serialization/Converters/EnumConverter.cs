// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

public class EnumConverter<T> : JsonConverter<T>
    where T : Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        foreach (var field in typeof(T).GetFields())
        {
            if (field.GetCustomAttribute<EnumMemberAttribute>() is EnumMemberAttribute attribute && attribute.Value?.Equals(value, StringComparison.OrdinalIgnoreCase) == true)
            {
                return (T)field.GetValue(null);
            }
        }

        throw new JsonException($"Unable to parse {value} to {typeof(T)}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = field.GetCustomAttribute<EnumMemberAttribute>();
        writer.WriteStringValue(attribute?.Value ?? value.ToString());
    }
}