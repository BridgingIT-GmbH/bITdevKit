// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Globalization;
using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;

/// <summary>Identifies ActiveEntity types when configuring BSON mappings without modifying Domain code.</summary>
public static class LiteDbActiveEntityMapping
{
    /// <summary>Checks whether the type inherits from ActiveEntity.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>True for an ActiveEntity type or subclass.</returns>
    public static bool IsActiveEntity(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ActiveEntity<,>))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Maps primitive and generated EntityId identities to scalar BSON values.</summary>
/// <typeparam name="TId">A Guid, string, integer, long, or EntityId over one of those types.</typeparam>
/// <remarks>Generated typed IDs must expose Value and a public static Create method accepting their primitive type.</remarks>
public static class LiteDbActiveEntityMapping<TId>
{
    private static readonly PropertyInfo Value = typeof(TId).GetProperty("Value");

    /// <summary>Gets the BSON-compatible primitive type represented by the identifier.</summary>
    public static Type PrimitiveType { get; } = GetPrimitiveType();

    /// <summary>Registers scalar serialization and reconstruction for a typed identifier.</summary>
    /// <param name="mapper">The database's private mapper, before database use.</param>
    public static void Configure(BsonMapper mapper)
    {
        if (PrimitiveType != typeof(TId))
        {
            var create = typeof(TId).GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [PrimitiveType])
                ?? throw new ArgumentException($"Typed identifier {typeof(TId).Name} needs a public Create method.");
            mapper.RegisterType(typeof(TId), id => ToBson(id), bson => create.Invoke(null, [bson.RawValue]));
        }
    }

    /// <summary>Gets the native auto-allocation mode, preserving Int32 and Int64 identity types.</summary>
    public static BsonAutoId AutoId => PrimitiveType == typeof(int) ? BsonAutoId.Int32
        : PrimitiveType == typeof(long) ? BsonAutoId.Int64 : BsonAutoId.Guid;

    /// <summary>Checks for null, an empty string or GUID, or a zero numeric identity.</summary>
    /// <param name="id">The primitive or typed identity.</param>
    /// <returns>True when a new identity must be allocated.</returns>
    public static bool IsDefault(object id)
    {
        var value = id is TId && Value is not null ? Value.GetValue(id) : id;
        return value is null or 0 or 0L or "" || value is Guid guid && guid == Guid.Empty;
    }

    /// <summary>Normalizes a primitive, typed, or string-represented identity before native query translation.</summary>
    /// <param name="id">The identity to normalize.</param>
    /// <returns>The scalar BSON identity.</returns>
    /// <exception cref="LiteDbProviderException">The identity is missing.</exception>
    /// <exception cref="FormatException">A string cannot be parsed as the required primitive identity.</exception>
    public static BsonValue ToBson(object id)
    {
        if (id is null)
        {
            throw new LiteDbProviderException(new InvalidInputError("An identifier is required."));
        }

        var value = id is TId && Value is not null ? Value.GetValue(id) : id;
        if (value is null)
        {
            throw new LiteDbProviderException(new InvalidInputError("An identifier is required."));
        }

        if (PrimitiveType == typeof(Guid))
        {
            return value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        if (PrimitiveType == typeof(int))
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        if (PrimitiveType == typeof(long))
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>Restores a primitive or typed identity from a stored scalar value.</summary>
    /// <param name="id">The native identity returned by LiteDB.</param>
    /// <returns>The identity with its declared CLR type.</returns>
    public static TId FromBson(BsonValue id)
    {
        if (PrimitiveType == typeof(TId))
        {
            return (TId)id.RawValue;
        }

        return (TId)typeof(TId).GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [PrimitiveType])!
            .Invoke(null, [id.RawValue]);
    }

    /// <summary>Sets a supplied or generated GUID/string identity, or leaves numeric allocation to LiteDB.</summary>
    /// <param name="entity">The entity whose identity is being inserted.</param>
    /// <param name="document">The BSON document to prepare before insertion.</param>
    /// <remarks>Assign the native insertion result back to the entity through FromBson after insertion succeeds.</remarks>
    public static void PrepareId(IEntity entity, BsonDocument document)
    {
        if (!IsDefault(entity.Id))
        {
            document["_id"] = ToBson(entity.Id);
        }
        else if (PrimitiveType == typeof(Guid) || PrimitiveType == typeof(string))
        {
            var guid = GuidGenerator.CreateSequential();
            document["_id"] = PrimitiveType == typeof(Guid) ? new BsonValue(guid) : new BsonValue(guid.ToString());
        }
        else
        {
            document.Remove("_id");
        }
    }

    private static Type GetPrimitiveType()
    {
        var type = typeof(TId);
        if (Value is not null)
        {
            var current = type.BaseType;
            while (current is not null && !(current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EntityId<>)))
            {
                current = current.BaseType;
            }

            if (current is not null)
            {
                type = Value.PropertyType;
            }
        }

        if (type != typeof(Guid) && type != typeof(string) && type != typeof(int) && type != typeof(long))
        {
            throw new ArgumentException($"LiteDB identifier type {typeof(TId).Name} is not supported.");
        }

        return type;
    }
}
