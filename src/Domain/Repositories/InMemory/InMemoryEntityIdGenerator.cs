// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using System;
using System.Reflection;

public class InMemoryEntityIdGenerator<TEntity>(InMemoryContext<TEntity> context) : IEntityIdGenerator<TEntity>
    where TEntity : class, IEntity
{
    private readonly InMemoryContext<TEntity> context = context;

    public bool IsNew(object id)
    {
        return id switch
        {
            null => true,
            int e => e == 0,
            long e => e == 0,
            string e => e.IsNullOrEmpty(),
            Guid e => e == Guid.Empty,
            EntityId<Guid> e => e.Value == Guid.Empty,
            EntityId<int> e => e.Value == 0,
            EntityId<long> e => e.Value == 0,
            EntityId<string> e => e.Value.IsNullOrEmpty(),
            _ => IsTypedIdType(id.GetType())
                 ? IsTypedIdEmpty(id)
                 : throw new NotSupportedException($"Entity ID type {id.GetType().Name} not supported")
        };
    }

    public void SetNew(TEntity entity)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var idProperty = typeof(TEntity).GetProperty("Id") ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} does not have an Id property.");
        var idType = idProperty.PropertyType;

        // Handle primitive ID types
        if (idType == typeof(int))
        {
            entity.Id = this.context.Entities.Count + 1;
        }
        else if (idType == typeof(long))
        {
            entity.Id = this.context.Entities.Count + 1;
        }
        else if (idType == typeof(string))
        {
            entity.Id = GuidGenerator.CreateSequential().ToString();
        }
        else if (idType == typeof(Guid))
        {
            entity.Id = GuidGenerator.CreateSequential();
        }
        // Handle typed IDs (e.g., CustomerId, OrderId)
        else if (IsTypedIdType(idType))
        {
            // Get the underlying type of the EntityId<T> (e.g., Guid for CustomerId)
            var underlyingType = (idType.GetProperty("Value")?.PropertyType) ?? throw new NotSupportedException($"Typed ID {idType.Name} does not have a Value property.");

            // Generate the underlying value
            object underlyingValue = underlyingType switch
            {
                Type t when t == typeof(Guid) => GuidGenerator.CreateSequential(),
                Type t when t == typeof(int) => this.context.Entities.Count + 1,
                Type t when t == typeof(long) => this.context.Entities.Count + 1,
                Type t when t == typeof(string) => GuidGenerator.CreateSequential().ToString(),
                _ => throw new NotSupportedException($"Underlying ID type {underlyingType.Name} not supported for typed ID {idType.Name}")
            };

            // Find the Create method for the typed ID (e.g., CustomerId.Create(Guid))
            var createMethod = idType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [underlyingType]) ?? throw new NotSupportedException($"Typed ID {idType.Name} does not have a suitable Create method for type {underlyingType.Name}.");

            entity.Id = createMethod.Invoke(null, [underlyingValue]); // Invoke the Create method to instantiate the typed ID
        }
        else
        {
            throw new NotSupportedException($"Entity ID type {idType.Name} not supported");
        }
    }

    private static bool IsTypedIdType(Type idType)
    {
        var currentType = idType;
        while (currentType != null && currentType != typeof(object))
        {
            if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(EntityId<>))
            {
                return true;
            }
            currentType = currentType.BaseType;
        }
        return false;
    }

    private static bool IsTypedIdEmpty(object id)
    {
        if (id == null)
        {
            return true;
        }

        var valueProperty = id.GetType().GetProperty("Value");
        if (valueProperty == null)
        {
            return false; // Cannot determine emptiness without Value property
        }

        var value = valueProperty.GetValue(id);
        return value switch
        {
            null => true,
            int i => i == 0,
            long l => l == 0,
            string s => s.IsNullOrEmpty(),
            Guid g => g == Guid.Empty,
            _ => false // Default to non-empty for unknown types
        };
    }
}