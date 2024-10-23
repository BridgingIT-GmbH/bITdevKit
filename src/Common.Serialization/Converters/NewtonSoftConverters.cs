// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

public class EnumConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType.IsEnum;
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var value = reader.Value.ToString();
        foreach (var field in objectType.GetFields())
        {
            var attribute = field.GetCustomAttribute<System.Runtime.Serialization.EnumMemberAttribute>();
            if (attribute != null && attribute.Value == value)
            {
                return field.GetValue(null);
            }
        }

        throw new JsonSerializationException($"Unable to parse {value} to {objectType}");
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = field.GetCustomAttribute<System.Runtime.Serialization.EnumMemberAttribute>();
        writer.WriteValue(attribute?.Value ?? value.ToString());
    }
}

public class FilterCriteriaConverter : JsonConverter<FilterCriteria>
{
    public override FilterCriteria ReadJson(JsonReader reader, Type objectType, FilterCriteria existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var jObject = JObject.Load(reader);
        var filterCriteria = new FilterCriteria();

        foreach (var property in typeof(FilterCriteria).GetProperties())
        {
            if (jObject.TryGetValue(property.Name, out var token))
            {
                var value = token.ToObject(property.PropertyType, serializer);
                property.SetValue(filterCriteria, value);
            }
        }

        return filterCriteria;
    }

    public override void WriteJson(JsonWriter writer, FilterCriteria value, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        foreach (var property in typeof(FilterCriteria).GetProperties())
        {
            var propertyValue = property.GetValue(value);
            if (propertyValue != null)
            {
                writer.WritePropertyName(property.Name);
                serializer.Serialize(writer, propertyValue);
            }
        }

        writer.WriteEndObject();
    }
}