namespace BridgingIT.DevKit.Domain.Model;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Base converter for enumerations with default int id and string value.
/// </summary>
public class EnumerationJsonConverter<TEnumeration>
    : EnumerationJsonConverter<TEnumeration, int, string>
    where TEnumeration : IEnumeration
{ }

/// <summary>
///     Converter for enumerations with default int id and custom value type.
/// </summary>
public class EnumerationJsonConverter<TEnumeration, TValue>
    : EnumerationJsonConverter<TEnumeration, int, TValue>
    where TEnumeration : IEnumeration<TValue>
    where TValue : IComparable
{ }

/// <summary>
///     Converter for enumerations with custom id and value types.
/// </summary>
public class EnumerationJsonConverter<TEnumeration, TId, TValue> : JsonConverter<TEnumeration>
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

        var id = (TId)ReadValue(ref reader, typeof(TId));
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

    private void WriteValue(Utf8JsonWriter writer, TId value)
    {
        switch (value)
        {
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case byte byteValue:
                writer.WriteNumberValue(byteValue);
                break;
            case sbyte sbyteValue:
                writer.WriteNumberValue(sbyteValue);
                break;
            case char charValue:
                writer.WriteNumberValue(charValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                break;
            case float floatValue:
                writer.WriteNumberValue(floatValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case uint uintValue:
                writer.WriteNumberValue(uintValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case ulong ulongValue:
                writer.WriteNumberValue(ulongValue);
                break;
            case short shortValue:
                writer.WriteNumberValue(shortValue);
                break;
            case ushort ushortValue:
                writer.WriteNumberValue(ushortValue);
                break;
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case Guid guidValue:
                writer.WriteStringValue(guidValue);
                break;
            case DateTime dateTimeValue:
                writer.WriteStringValue(dateTimeValue);
                break;
            case DateTimeOffset dateTimeOffsetValue:
                writer.WriteStringValue(dateTimeOffsetValue);
                break;
            default:
                throw new JsonException($"Unsupported ID type: {value.GetType()}");
        }
    }
}

/// <summary>
///     Base converter for ICollection of enumerations with default int id and string value.
/// </summary>
public class EnumerationCollectionJsonConverter<TEnumeration>
    : EnumerationCollectionJsonConverter<TEnumeration, int, string>
    where TEnumeration : IEnumeration
{ }

/// <summary>
///     Converter for ICollection of enumerations with default int id and custom value type.
/// </summary>
public class EnumerationCollectionJsonConverter<TEnumeration, TValue>
    : EnumerationCollectionJsonConverter<TEnumeration, int, TValue>
    where TEnumeration : IEnumeration<TValue>
    where TValue : IComparable
{ }

/// <summary>
///     Converter for ICollection of enumerations with custom id and value types.
/// </summary>
public class EnumerationCollectionJsonConverter<TEnumeration, TId, TValue>
    : JsonConverter<ICollection<TEnumeration>>
    where TEnumeration : IEnumeration<TId, TValue>
    where TId : IComparable
    where TValue : IComparable
{
    private readonly EnumerationJsonConverter<TEnumeration, TId, TValue> itemConverter;

    public EnumerationCollectionJsonConverter()
    {
        this.itemConverter = new EnumerationJsonConverter<TEnumeration, TId, TValue>();
    }

    public override ICollection<TEnumeration> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array.");
        }

        var items = new List<TEnumeration>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            var item = this.itemConverter.Read(ref reader, typeof(TEnumeration), options);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    public override void Write(
        Utf8JsonWriter writer,
        ICollection<TEnumeration> value,
        JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();

        foreach (var item in value)
        {
            this.itemConverter.Write(writer, item, options);
        }

        writer.WriteEndArray();
    }
}

/// <summary>
///     Base converter for List of enumerations with default int id and string value.
/// </summary>
public class EnumerationListJsonConverter<TEnumeration>
    : EnumerationListJsonConverter<TEnumeration, int, string>
    where TEnumeration : IEnumeration
{ }

/// <summary>
///     Converter for List of enumerations with default int id and custom value type.
/// </summary>
public class EnumerationListJsonConverter<TEnumeration, TValue>
    : EnumerationListJsonConverter<TEnumeration, int, TValue>
    where TEnumeration : IEnumeration<TValue>
    where TValue : IComparable
{ }

/// <summary>
///     Converter for List of enumerations with custom id and value types.
/// </summary>
public class EnumerationListJsonConverter<TEnumeration, TId, TValue>
    : JsonConverter<List<TEnumeration>>
    where TEnumeration : IEnumeration<TId, TValue>
    where TId : IComparable
    where TValue : IComparable
{
    private readonly EnumerationJsonConverter<TEnumeration, TId, TValue> itemConverter;

    public EnumerationListJsonConverter()
    {
        this.itemConverter = new EnumerationJsonConverter<TEnumeration, TId, TValue>();
    }

    public override List<TEnumeration> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array.");
        }

        var items = new List<TEnumeration>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            var item = this.itemConverter.Read(ref reader, typeof(TEnumeration), options);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<TEnumeration> value,
        JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();

        foreach (var item in value)
        {
            this.itemConverter.Write(writer, item, options);
        }

        writer.WriteEndArray();
    }
}

/// <summary>
///     Base converter for HashSet of enumerations with default int id and string value.
/// </summary>
public class EnumerationSetJsonConverter<TEnumeration>
    : EnumerationSetJsonConverter<TEnumeration, int, string>
    where TEnumeration : IEnumeration
{ }

/// <summary>
///     Converter for HashSet of enumerations with default int id and custom value type.
/// </summary>
public class EnumerationSetJsonConverter<TEnumeration, TValue>
    : EnumerationSetJsonConverter<TEnumeration, int, TValue>
    where TEnumeration : IEnumeration<TValue>
    where TValue : IComparable
{ }

/// <summary>
///     Converter for HashSet of enumerations with custom id and value types.
/// </summary>
public class EnumerationSetJsonConverter<TEnumeration, TId, TValue>
    : JsonConverter<HashSet<TEnumeration>>
    where TEnumeration : IEnumeration<TId, TValue>
    where TId : IComparable
    where TValue : IComparable
{
    private readonly EnumerationJsonConverter<TEnumeration, TId, TValue> itemConverter;

    public EnumerationSetJsonConverter()
    {
        this.itemConverter = new EnumerationJsonConverter<TEnumeration, TId, TValue>();
    }

    public override HashSet<TEnumeration> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array.");
        }

        var items = new HashSet<TEnumeration>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            var item = this.itemConverter.Read(ref reader, typeof(TEnumeration), options);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    public override void Write(
        Utf8JsonWriter writer,
        HashSet<TEnumeration> value,
        JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();

        foreach (var item in value)
        {
            this.itemConverter.Write(writer, item, options);
        }

        writer.WriteEndArray();
    }
}