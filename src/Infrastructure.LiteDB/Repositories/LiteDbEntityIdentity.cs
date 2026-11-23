// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using System.Globalization;
using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>Normalizes the declared entity identity for repository mapping and native allocation.</summary>
public sealed class LiteDbEntityIdentity
{
    private readonly Type idType;
    private readonly PropertyInfo valueProperty;
    private readonly MethodInfo create;

    /// <summary>Creates the identity mapping for a primitive or EntityId type.</summary>
    /// <param name="idType">The entity's declared Id property type.</param>
    public LiteDbEntityIdentity(Type idType)
    {
        this.idType = idType ?? throw new ArgumentNullException(nameof(idType));
        for (var current = idType.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EntityId<>))
            {
                this.valueProperty = idType.GetProperty("Value");
                break;
            }
        }

        this.PrimitiveType = this.valueProperty?.PropertyType ?? idType;
        if (this.PrimitiveType != typeof(Guid) && this.PrimitiveType != typeof(string) && this.PrimitiveType != typeof(int) && this.PrimitiveType != typeof(long))
        {
            throw new ArgumentException($"LiteDB identity type {idType.Name} is not supported.", nameof(idType));
        }

        this.create = this.valueProperty is null ? null : idType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [this.PrimitiveType])
            ?? throw new ArgumentException("A typed identity must expose Create(primitive).", nameof(idType));
    }

    /// <summary>Gets the BSON-compatible primitive identity type.</summary>
    public Type PrimitiveType { get; }

    /// <summary>Gets the native numeric allocation mode, or Guid allocation for nonnumeric identities.</summary>
    public BsonAutoId AutoId => this.PrimitiveType == typeof(int) ? BsonAutoId.Int32 : this.PrimitiveType == typeof(long) ? BsonAutoId.Int64 : BsonAutoId.Guid;

    /// <summary>Configures scalar typed identity serialization before the mapper is used.</summary>
    /// <param name="mapper">The database mapper.</param>
    public void Configure(BsonMapper mapper)
    {
        if (this.create is not null)
        {
            mapper.RegisterType(this.idType, value => this.ToBson(value), value => this.FromBson(value));
        }
    }

    /// <summary>Checks whether the identity needs allocation.</summary>
    /// <param name="id">The primitive or typed identity.</param>
    /// <returns>True for null, empty, or zero identities.</returns>
    public bool IsDefault(object id)
    {
        var value = this.Unwrap(id);
        return value is null or "" or 0 or 0L || value is Guid guid && guid == Guid.Empty;
    }

    /// <summary>Converts a typed, primitive, or string-represented identity to BSON.</summary>
    /// <param name="id">The non-null identity.</param>
    /// <returns>The native identity value.</returns>
    public BsonValue ToBson(object id)
    {
        var value = this.Unwrap(id) ?? throw new ArgumentNullException(nameof(id));
        if (this.PrimitiveType == typeof(Guid)) { return value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)); }

        if (this.PrimitiveType == typeof(int)) { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }

        if (this.PrimitiveType == typeof(long)) { return Convert.ToInt64(value, CultureInfo.InvariantCulture); }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>Restores the declared CLR identity from a native value.</summary>
    /// <param name="id">The native value.</param>
    /// <returns>A primitive or typed CLR identity.</returns>
    public object FromBson(BsonValue id) => this.create is null ? id.RawValue : this.create.Invoke(null, [id.RawValue]);

    /// <summary>Prepares the identity of a BSON document for insertion.</summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="document">Its BSON document.</param>
    public void Prepare(IEntity entity, BsonDocument document)
    {
        if (!this.IsDefault(entity.Id)) { document["_id"] = this.ToBson(entity.Id); }
        else if (this.PrimitiveType == typeof(Guid)) { document["_id"] = GuidGenerator.CreateSequential(); }
        else if (this.PrimitiveType == typeof(string)) { document["_id"] = GuidGenerator.CreateSequential().ToString(); }
        else { document.Remove("_id"); }
    }

    private object Unwrap(object id) => id is not null && this.valueProperty is not null && this.idType.IsInstanceOfType(id) ? this.valueProperty.GetValue(id) : id;
}
