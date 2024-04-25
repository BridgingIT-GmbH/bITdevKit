// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Storage;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using global::Azure.Data.Tables;

public static class AzureTableEntityConverter
{
    private static JsonSerializerOptions defaultJsonSerializerOptions = new();

    public static void SetDefaultJsonSerializerOptions(
        JsonSerializerOptions jsonSerializerOptions = default)
    {
        defaultJsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions();
    }

    public static TableEntity ToTableEntity<T>(
        this T entity,
        string partitionKey,
        string rowKey,
        JsonSerializerOptions jsonSerializerOptions = default,
        PropertyConverters<T> propertyConverters = default)
        where T : class, new()
    {
        return CreateTableEntity(
            entity,
            typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.CanWrite).ToList(),
            partitionKey,
            rowKey,
            jsonSerializerOptions ?? defaultJsonSerializerOptions,
            propertyConverters);
    }

    public static T FromTableEntity<T>(
        this TableEntity tableEntity,
        JsonSerializerOptions jsonSerializerOptions = default,
        PropertyConverters<T> propertyConverters = default)
        where T : class, new()
    {
        var entity = new T();

        FillEntityProperties(
            tableEntity,
            entity,
            typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.CanWrite).ToList(),
            jsonSerializerOptions ?? defaultJsonSerializerOptions,
            propertyConverters);

        return entity;
    }

    private static TableEntity CreateTableEntity<T>(
        object entity,
        IEnumerable<PropertyInfo> properties,
        string partitionKey,
        string rowKey,
        JsonSerializerOptions jsonSerializerOptions,
        PropertyConverters<T> propertyConverters)
        where T : class, new()
    {
        EnsureArg.IsNotNull(jsonSerializerOptions, nameof(jsonSerializerOptions));

        var tableEntity = new TableEntity(partitionKey, rowKey);
        foreach (var propertyInfo in properties)
        {
            var name = propertyInfo.Name;
            var value = propertyInfo.GetValue(entity);
            object entityProperty;

            if (propertyConverters is not null && propertyConverters.ContainsKey(name))
            {
                entityProperty = propertyConverters[name].ToTableEntityProperty((T)entity);
            }
            else
            {
                switch (value)
                {
                    case int x:
                        entityProperty = x;
                        break;
                    case short x:
                        entityProperty = x;
                        break;
                    case byte x:
                        entityProperty = x;
                        break;
                    case string x:
                        entityProperty = x;
                        break;
                    case double x:
                        entityProperty = x;
                        break;
                    case DateTime x:
                        entityProperty = x;
                        break;
                    case DateTimeOffset x:
                        entityProperty = x;
                        break;
                    case bool x:
                        entityProperty = x;
                        break;
                    case byte[] x:
                        entityProperty = x;
                        break;
                    case long x:
                        entityProperty = x;
                        break;
                    case Guid x:
                        entityProperty = x;
                        break;
                    case null:
                        entityProperty = null;
                        break;
                    default:
                        name += "Json";
                        entityProperty = JsonSerializer.Serialize(value, jsonSerializerOptions);
                        break;
                }
            }

            tableEntity[name] = entityProperty;
        }

        return tableEntity;
    }

    private static void FillEntityProperties<T>(
        TableEntity tableEntity,
        T entity,
        IEnumerable<PropertyInfo> properties,
        JsonSerializerOptions jsonSerializerOptions,
        PropertyConverters<T> propertyConverters)
        where T : class, new()
    {
        EnsureArg.IsNotNull(tableEntity, nameof(tableEntity));
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNull(jsonSerializerOptions, nameof(jsonSerializerOptions));

        foreach (var propertyInfo in properties)
        {
            if (propertyConverters is not null && tableEntity.Keys.Contains(propertyInfo.Name) && propertyConverters.ContainsKey(propertyInfo.Name))
            {
                propertyConverters[propertyInfo.Name].SetObjectProperty(entity, tableEntity[propertyInfo.Name]);
            }
            else if (tableEntity.ContainsKey(propertyInfo.Name))
            {
                var value = tableEntity[propertyInfo.Name];

                if (value is not null && (propertyInfo.PropertyType == typeof(DateTimeOffset) || propertyInfo.PropertyType == typeof(DateTimeOffset?)))
                {
                    value = tableEntity.GetDateTimeOffset(propertyInfo.Name);
                }

                if (value is not null && propertyInfo.PropertyType == typeof(double))
                {
                    value = tableEntity.GetDouble(propertyInfo.Name);
                }

                if (value is not null && propertyInfo.PropertyType == typeof(int))
                {
                    value = tableEntity.GetInt32(propertyInfo.Name);
                }

                if (value is not null && propertyInfo.PropertyType == typeof(long))
                {
                    value = tableEntity.GetInt64(propertyInfo.Name);
                }

                if (value is not null && propertyInfo.PropertyType == typeof(Guid))
                {
                    value = tableEntity.GetGuid(propertyInfo.Name);
                }

                propertyInfo.SetValue(entity, value);
            }
            else if (tableEntity.ContainsKey($"{propertyInfo.Name}Json"))
            {
                var value = tableEntity.GetString($"{propertyInfo.Name}Json");
                if (value is not null)
                {
                    propertyInfo.SetValue(
                        entity,
                        JsonSerializer.Deserialize(value, propertyInfo.PropertyType, jsonSerializerOptions));
                }
            }
        }
    }
}

public class PropertyConverter<T>
{
    public PropertyConverter(Func<T, object> toTableEntityProperty, Action<T, object> setObjectProperty)
    {
        this.ToTableEntityProperty = toTableEntityProperty;
        this.SetObjectProperty = setObjectProperty;
    }

    public Func<T, object> ToTableEntityProperty { get; }
    public Action<T, object> SetObjectProperty { get; }
}

public class PropertyConverters<T> : Dictionary<string, PropertyConverter<T>>
{
}