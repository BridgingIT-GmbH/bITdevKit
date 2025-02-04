// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

public class EnumMemberConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private readonly Dictionary<string, T> nameToEnum;
    private readonly Dictionary<T, string> enumToName;

    public EnumMemberConverter()
    {
        this.nameToEnum = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        this.enumToName = new Dictionary<T, string>();

        var type = typeof(T);
        var values = Enum.GetValues<T>();

        foreach (var value in values)
        {
            var memInfo = type.GetMember(value.ToString())[0];
            var enumMemberAttr = memInfo.GetCustomAttribute<EnumMemberAttribute>();

            var enumName = enumMemberAttr?.Value ?? value.ToString();
            this.nameToEnum[enumName] = value;
            this.nameToEnum[value.ToString()] = value; // Allow both enum name and EnumMember value
            this.enumToName[value] = enumName;
        }
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string value for enum {typeof(T).Name}");
        }

        var enumString = reader.GetString();
        return this.nameToEnum.TryGetValue(enumString, out var enumValue)
            ? enumValue
            : throw new JsonException($"Invalid enum value '{enumString}' for {typeof(T).Name}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(this.enumToName[value]);
    }
}