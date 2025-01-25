// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Converts enumeration values to and from their JSON representation using System.Text.Json.
/// </summary>
/// <typeparam name="TEnumeration">The enumeration type to be converted.</typeparam>
public class EnumerationSystemTextJsonConverter<TEnumeration>
    : EnumerationSystemTextJsonConverter<TEnumeration, int, string>
    where TEnumeration : IEnumeration
{ }

/// <summary>
///     Custom JSON converter for enumerations that implements the System.Text.Json.Serialization.JsonConverter
///     to handle the serialization and deserialization of enumeration types.
/// </summary>
/// <typeparam name="TEnumeration">The enumeration type to be converted.</typeparam>
/// <typeparam name="TValue">The type of the enumeration value.</typeparam>
public class EnumerationSystemTextJsonConverter<TEnumeration, TValue>
    : EnumerationSystemTextJsonConverter<TEnumeration, int, TValue>
    where TEnumeration : IEnumeration<TValue>
    where TValue : IComparable
{ }

/// <summary>
///     Converts enumeration types to and from JSON using System.Text.Json. This converter can handle cases
///     where the enumeration type is represented by a generic identifier and value type.
/// </summary>
/// <typeparam name="TEnumeration">The enumeration type to convert.</typeparam>
/// <typeparam name="TId">The type of the identifier.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
public class EnumerationSystemTextJsonConverter<TEnumeration, TId, TValue> : JsonConverter<TEnumeration>
    where TEnumeration : IEnumeration<TId, TValue>
    where TId : IComparable
    where TValue : IComparable
{
    /// <summary>
    ///     Reads and converts the JSON to the specified enumeration type.
    /// </summary>
    /// <param name="reader">The reader used to read the JSON.</param>
    /// <param name="typeToConvert">The type being converted.</param>
    /// <param name="options">Options to control the serialization process.</param>
    /// <returns>The enumeration type instance that corresponds to the JSON.</returns>
    public override TEnumeration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        var id = (TId)ReadValue(ref reader, typeof(TId));

        return Enumeration<TId, TValue>.FromId<TEnumeration>(id);
    }

    /// <summary>
    ///     Writes the JSON representation of the specified Enumeration object.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of the Enumeration.</typeparam>
    /// <typeparam name="TId">The type of the Enumeration ID.</typeparam>
    /// <typeparam name="TValue">The type of the Enumeration Value.</typeparam>
    /// <param name="writer">The writer to which the JSON will be written.</param>
    /// <param name="value">The Enumeration object to write as JSON.</param>
    /// <param name="options">Options to control the serialization behavior.</param>
    public override void Write(Utf8JsonWriter writer, TEnumeration value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();

            return;
        }

        this.WriteValue(writer, value.Id);
    }

    /// <summary>
    ///     Reads a value from the JSON reader based on the specified type.
    /// </summary>
    /// <param name="reader">The Utf8JsonReader to read from.</param>
    /// <param name="type">The type of the value to read.</param>
    /// <returns>The object value read from the reader.</returns>
    /// <exception cref="JsonException">Thrown if the type is not supported.</exception>
    private static object ReadValue(ref Utf8JsonReader reader, Type type)
    {
        if (type == typeof(bool))
        {
            return reader.GetBoolean();
        }

        if (type == typeof(byte))
        {
            return reader.GetByte();
        }

        if (type == typeof(sbyte))
        {
            return reader.GetSByte();
        }

        if (type == typeof(char))
        {
            return (char)reader.GetUInt16();
        }

        if (type == typeof(decimal))
        {
            return reader.GetDecimal();
        }

        if (type == typeof(double))
        {
            return reader.GetDouble();
        }

        if (type == typeof(float))
        {
            return reader.GetSingle();
        }

        if (type == typeof(int))
        {
            return reader.GetInt32();
        }

        if (type == typeof(uint))
        {
            return reader.GetUInt32();
        }

        if (type == typeof(long))
        {
            return reader.GetInt64();
        }

        if (type == typeof(ulong))
        {
            return reader.GetUInt64();
        }

        if (type == typeof(short))
        {
            return reader.GetInt16();
        }

        if (type == typeof(ushort))
        {
            return reader.GetUInt16();
        }

        if (type == typeof(string))
        {
            return reader.GetString();
        }

        if (type == typeof(Guid))
        {
            return reader.GetGuid();
        }

        if (type == typeof(DateTime))
        {
            return reader.GetDateTime();
        }

        if (type == typeof(DateTimeOffset))
        {
            return reader.GetDateTimeOffset();
        }

        throw new JsonException($"Unsupported ID type: {type}");
    }

    /// <summary>
    ///     Writes a value to a Utf8JsonWriter based on the provided type.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to which the value will be written.</param>
    /// <param name="value">
    ///     The value to write, where the type of value can be bool, byte, sbyte, char,
    ///     decimal, double, float, int, uint, long, ulong, short, ushort, string, Guid, DateTime,
    ///     or DateTimeOffset.
    /// </param>
    /// <exception cref="JsonException">Thrown when the type of the provided value is unsupported.</exception>
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