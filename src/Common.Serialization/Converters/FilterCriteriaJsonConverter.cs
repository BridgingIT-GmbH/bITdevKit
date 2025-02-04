// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;
/// <summary>
/// A custom JSON converter for the <see cref="FilterCriteria"/> class.
/// </summary>
/// <remarks>
/// This converter handles the serialization and deserialization of <see cref="FilterCriteria"/>
/// objects to and from JSON, using the System.Text.Json library.
/// </remarks>
public class FilterCriteriaJsonConverter : JsonConverter<FilterCriteria>
{
    /// <summary>
    /// Reads and converts the JSON to an instance of <see cref="FilterCriteria"/>.
    /// </summary>
    /// <param name="reader">The reader instance used to read the JSON.</param>
    /// <param name="typeToConvert">The type of the object to convert.</param>
    /// <param name="options">The serializer options to use.</param>
    /// <returns>A <see cref="FilterCriteria"/> instance converted from the JSON.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the JSON is not valid or cannot be properly deserialized into a <see cref="FilterCriteria"/> object.
    /// </exception>
    public override FilterCriteria Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        var filterCriteria = new FilterCriteria();
        var enumConverter = new EnumMemberConverter<FilterOperator>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return filterCriteria;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            var field = reader.GetString();
            var normalizedPropertyName = char.ToUpper(field[0]) + field[1..];
            reader.Read();

            switch (normalizedPropertyName)
            {
                case nameof(FilterCriteria.Field):
                    filterCriteria.Field = reader.GetString();

                    break;
                case nameof(FilterCriteria.Operator):
                    filterCriteria.Operator = enumConverter.Read(ref reader, typeof(FilterOperator), options);

                    break;
                case nameof(FilterCriteria.Value):
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        if (reader.TryGetInt32(out var intValue))
                        {
                            filterCriteria.Value = intValue;
                        }
                        else if (reader.TryGetInt64(out var longValue))
                        {
                            filterCriteria.Value = longValue;
                        }
                        else if (reader.TryGetDouble(out var doubleValue))
                        {
                            filterCriteria.Value = doubleValue;
                        }
                        else
                        {
                            filterCriteria.Value = reader.GetDecimal();
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.String)
                    {
                        filterCriteria.Value = reader.GetString();
                    }
                    else if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                    {
                        filterCriteria.Value = reader.GetBoolean();
                    }
                    else if (reader.TokenType == JsonTokenType.Null)
                    {
                        filterCriteria.Value = null;
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        filterCriteria.Value = JsonSerializer.Deserialize<object[]>(ref reader, options);
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        filterCriteria.Value = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
                    }

                    break;
                case nameof(FilterCriteria.Logic):
                    filterCriteria.Logic = JsonSerializer.Deserialize<FilterLogicOperator>(ref reader, options);

                    break;
                case nameof(FilterCriteria.Filters):
                    filterCriteria.Filters = JsonSerializer.Deserialize<List<FilterCriteria>>(ref reader, options);

                    break;
                case nameof(FilterCriteria.CustomType):
                    filterCriteria.CustomType = JsonSerializer.Deserialize<FilterCustomType>(ref reader, options);

                    break;
                case nameof(FilterCriteria.CustomParameters):
                    filterCriteria.CustomParameters = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);

                    break;
                case nameof(FilterCriteria.SpecificationName):
                    filterCriteria.SpecificationName = reader.GetString();

                    break;
                case nameof(FilterCriteria.SpecificationArguments):
                    filterCriteria.SpecificationArguments = JsonSerializer.Deserialize<object[]>(ref reader, options);

                    break;
                case nameof(FilterCriteria.CompositeSpecification):
                    filterCriteria.CompositeSpecification = JsonSerializer.Deserialize<CompositeSpecification>(ref reader, options);

                    break;
            }
        }

        throw new JsonException();
    }

    /// <summary>
    /// Writes the <see cref="FilterCriteria"/> object to JSON using the specified <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> used to write the JSON output.</param>
    /// <param name="value">The <see cref="FilterCriteria"/> instance to be serialized.</param>
    /// <param name="options">Options to control the behavior during writing to JSON.</param>
    public override void Write(Utf8JsonWriter writer, FilterCriteria value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString(nameof(FilterCriteria.Field), value.Field);
        writer.WritePropertyName(nameof(FilterCriteria.Operator));
        JsonSerializer.Serialize(writer, value.Operator, options);
        writer.WritePropertyName(nameof(FilterCriteria.Value));
        JsonSerializer.Serialize(writer, value.Value, options);
        writer.WritePropertyName(nameof(FilterCriteria.Logic));
        JsonSerializer.Serialize(writer, value.Logic, options);

        if (value.Filters?.Any() == true)
        {
            writer.WritePropertyName(nameof(FilterCriteria.Filters));
            JsonSerializer.Serialize(writer, value.Filters, options);
        }

        writer.WritePropertyName(nameof(FilterCriteria.CustomType));
        JsonSerializer.Serialize(writer, value.CustomType, options);

        if (value.CustomParameters?.Any() == true)
        {
            writer.WritePropertyName(nameof(FilterCriteria.CustomParameters));
            JsonSerializer.Serialize(writer, value.CustomParameters, options);
        }

        if (!string.IsNullOrEmpty(value.SpecificationName))
        {
            writer.WriteString(nameof(FilterCriteria.SpecificationName), value.SpecificationName);
        }

        if (value.SpecificationArguments?.Any() == true)
        {
            writer.WritePropertyName(nameof(FilterCriteria.SpecificationArguments));
            JsonSerializer.Serialize(writer, value.SpecificationArguments, options);
        }

        if (value.CompositeSpecification != null)
        {
            writer.WritePropertyName(nameof(FilterCriteria.CompositeSpecification));
            JsonSerializer.Serialize(writer, value.CompositeSpecification, options);
        }

        writer.WriteEndObject();
    }
}