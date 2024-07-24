// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class EnumerationSystemTextJsonConverter<TEnumeration>
    : EnumerationSystemTextJsonConverter<TEnumeration, int, string>
    where TEnumeration : IEnumeration
{
}

public class EnumerationSystemTextJsonConverter<TEnumeration, TValue>
    : EnumerationSystemTextJsonConverter<TEnumeration, int, TValue>
    where TEnumeration : IEnumeration<TValue>
    where TValue : IComparable
{
}

public class EnumerationSystemTextJsonConverter<TEnumeration, TId, TValue>
    : JsonConverter<TEnumeration>
    where TEnumeration : IEnumeration<TId, TValue>
    where TId : IComparable
    where TValue : IComparable
{
    public override TEnumeration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        var id = (TId)this.ReadValue(ref reader, typeof(TId));
        return Enumeration<TId, TValue>.FromId<TEnumeration>(id);
    }

    public override void Write(Utf8JsonWriter writer, TEnumeration value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        this.WriteValue(writer, value.Id);
    }

    private object ReadValue(ref Utf8JsonReader reader, Type type)
    {
        if (type == typeof(bool))
        {
            return reader.GetBoolean();
        }
        else if (type == typeof(byte))
        {
            return reader.GetByte();
        }
        else if (type == typeof(sbyte))
        {
            return (sbyte)reader.GetSByte();
        }
        else if (type == typeof(char))
        {
            return (char)reader.GetUInt16();
        }
        else if (type == typeof(decimal))
        {
            return reader.GetDecimal();
        }
        else if (type == typeof(double))
        {
            return reader.GetDouble();
        }
        else if (type == typeof(float))
        {
            return reader.GetSingle();
        }
        else if (type == typeof(int))
        {
            return reader.GetInt32();
        }
        else if (type == typeof(uint))
        {
            return reader.GetUInt32();
        }
        else if (type == typeof(long))
        {
            return reader.GetInt64();
        }
        else if (type == typeof(ulong))
        {
            return reader.GetUInt64();
        }
        else if (type == typeof(short))
        {
            return reader.GetInt16();
        }
        else if (type == typeof(ushort))
        {
            return reader.GetUInt16();
        }
        else if (type == typeof(string))
        {
            return reader.GetString();
        }
        else if (type == typeof(Guid))
        {
            return reader.GetGuid();
        }
        else if (type == typeof(DateTime))
        {
            return reader.GetDateTime();
        }
        else if (type == typeof(DateTimeOffset))
        {
            return reader.GetDateTimeOffset();
        }
        else
        {
            throw new JsonException($"Unsupported ID type: {type}");
        }
    }

    private void WriteValue(Utf8JsonWriter writer, TId value)
    {
        if (value is bool boolValue)
        {
            writer.WriteBooleanValue(boolValue);
        }
        else if (value is byte byteValue)
        {
            writer.WriteNumberValue(byteValue);
        }
        else if (value is sbyte sbyteValue)
        {
            writer.WriteNumberValue(sbyteValue);
        }
        else if (value is char charValue)
        {
            writer.WriteNumberValue(charValue);
        }
        else if (value is decimal decimalValue)
        {
            writer.WriteNumberValue(decimalValue);
        }
        else if (value is double doubleValue)
        {
            writer.WriteNumberValue(doubleValue);
        }
        else if (value is float floatValue)
        {
            writer.WriteNumberValue(floatValue);
        }
        else if (value is int intValue)
        {
            writer.WriteNumberValue(intValue);
        }
        else if (value is uint uintValue)
        {
            writer.WriteNumberValue(uintValue);
        }
        else if (value is long longValue)
        {
            writer.WriteNumberValue(longValue);
        }
        else if (value is ulong ulongValue)
        {
            writer.WriteNumberValue(ulongValue);
        }
        else if (value is short shortValue)
        {
            writer.WriteNumberValue(shortValue);
        }
        else if (value is ushort ushortValue)
        {
            writer.WriteNumberValue(ushortValue);
        }
        else if (value is string stringValue)
        {
            writer.WriteStringValue(stringValue);
        }
        else if (value is Guid guidValue)
        {
            writer.WriteStringValue(guidValue);
        }
        else if (value is DateTime dateTimeValue)
        {
            writer.WriteStringValue(dateTimeValue);
        }
        else if (value is DateTimeOffset dateTimeOffsetValue)
        {
            writer.WriteStringValue(dateTimeOffsetValue);
        }
        else
        {
            throw new JsonException($"Unsupported ID type: {value.GetType()}");
        }
    }
}