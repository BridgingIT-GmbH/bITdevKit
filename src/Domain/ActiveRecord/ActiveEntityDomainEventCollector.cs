// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Collects and publishes domain events from ActiveEntity entities and their object graphs.
/// </summary>
public class ActiveEntityDomainEventCollector // TODO: or publisher?
{
    /// <summary>
    /// Publishes all domain events for the entity and its child ActiveEntity properties/collections.
    /// </summary>
    public async Task<Result> PublishAllAsync<TEntity, TId>(
        ActiveEntity<TEntity, TId> entity,
        IDomainEventPublisher publisher,
        ActiveEntityDomainEventPublishOptions options = null,
        CancellationToken cancellationToken = default)
        where TEntity : ActiveEntity<TEntity, TId>
    {
        if (entity == null || publisher == null)
        {
            return Result.Success();
        }

        options ??= new ActiveEntityDomainEventPublishOptions();

        // recursive collection of events
        foreach (var @event in this.CollectAll(entity, options.ReverseOrder))
        {
            try
            {
                var publishResult = await publisher.Send(@event, cancellationToken).AnyContext();
                if (!publishResult.IsSuccess)
                {
                    return Result.Failure().WithErrors(publishResult.Errors);
                }
            }
            catch (Exception ex)
            {
                return Result.Failure().WithError(ex);
            }
        }

        if (options.ClearEvents)
        {
            this.ClearAll(entity);
        }

        return Result.Success();
    }

    /// <summary>
    /// Publishes all domain events using a notifier.
    /// </summary>
    public async Task<Result> PublishAllAsync<TEntity, TId>(
        ActiveEntity<TEntity, TId> entity,
        INotifier notifier,
        ActiveEntityDomainEventPublishOptions options = null,
        CancellationToken cancellationToken = default)
        where TEntity : ActiveEntity<TEntity, TId>
    {
        if (entity == null || notifier == null)
        {
            return Result.Success();
        }

        options ??= new ActiveEntityDomainEventPublishOptions();

        // recursive collection of events
        foreach (var @event in this.CollectAll(entity, options.ReverseOrder))
        {
            try
            {
                var publishResult = await notifier.PublishDynamicAsync(@event, null, cancellationToken).AnyContext();
                if (!publishResult.IsSuccess)
                {
                    return Result.Failure().WithErrors(publishResult.Errors);
                }
            }
            catch (Exception ex)
            {
                return Result.Failure().WithError(ex);
            }
        }

        if (options.ClearEvents)
        {
            this.ClearAll(entity);
        }

        return Result.Success();
    }

    /// <summary>
    /// Collects all domain events from the entity and its object graph.
    /// </summary>
    public List<IDomainEvent> CollectAll<TEntity, TId>(
        ActiveEntity<TEntity, TId> entity,
        bool reverseOrder = false)
        where TEntity : ActiveEntity<TEntity, TId>
    {
        var events = new List<IDomainEvent>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        this.CollectRecursive<TEntity, TId>(entity, events, visited, reverseOrder);

        return events;
    }

    /// <summary>
    /// Clears all domain events from the entity and its object graph.
    /// </summary>
    public void ClearAll<TEntity, TId>(ActiveEntity<TEntity, TId> entity)
        where TEntity : ActiveEntity<TEntity, TId>
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        this.ClearRecursive<TEntity, TId>(entity, visited);
    }

    private void CollectRecursive<TEntity, TId>(
        object entity,
        List<IDomainEvent> events,
        HashSet<object> visited,
        bool reverseOrder)
        where TEntity : ActiveEntity<TEntity, TId>
    {
        if (entity == null || visited.Contains(entity))
        {
            return;
        }

        visited.Add(entity);

        if (entity is ActiveEntity<TEntity, TId> activeEntity)
        {
            // Add parent events first if not reverse order
            if (!reverseOrder)
            {
                events.AddRange(activeEntity.DomainEvents);
            }

            // Process child entities
            foreach (var prop in ReflectionHelper.GetCachedProperties(entity.GetType()))
            {
                var value = prop.GetValue(entity);
                if (value == null)
                {
                    continue;
                }

                if (IsActiveEntityType(prop.PropertyType))
                {
                    this.CollectRecursive<TEntity, TId>(value, events, visited, reverseOrder);
                }
                else if (IsActiveEntityTypeCollection(prop.PropertyType))
                {
                    if (value is IEnumerable<object> collection)
                    {
                        foreach (var item in collection)
                        {
                            this.CollectRecursive<TEntity, TId>(item, events, visited, reverseOrder);
                        }
                    }
                }
            }

            // Add parent events last if reverse order
            if (reverseOrder)
            {
                events.AddRange(activeEntity.DomainEvents);
            }
        }
    }

    private void ClearRecursive<TEntity, TId>(object entity, HashSet<object> visited)
        where TEntity : ActiveEntity<TEntity, TId>
    {
        if (entity == null || visited.Contains(entity))
        {
            return;
        }

        visited.Add(entity);

        if (entity is ActiveEntity<TEntity, TId> activeEntity)
        {
            activeEntity.ClearDomainEvents();

            // Process child entities
            foreach (var prop in ReflectionHelper.GetCachedProperties(entity.GetType()))
            {
                var value = prop.GetValue(entity);
                if (value == null)
                {
                    continue;
                }

                if (IsActiveEntityType(prop.PropertyType))
                {
                    this.ClearRecursive<TEntity, TId>(value, visited);
                }
                else if (IsActiveEntityTypeCollection(prop.PropertyType))
                {
                    if (value is IEnumerable<object> collection)
                    {
                        foreach (var item in collection)
                        {
                            this.ClearRecursive<TEntity, TId>(item, visited);
                        }
                    }
                }
            }
        }
    }

    private static bool IsActiveEntityType(Type type)
    {
        if (type?.IsClass != true || type.IsAbstract)
        {
            return false;
        }

        var baseType = type;
        while (baseType != null)
        {
            if (baseType.IsGenericType &&
                baseType.GetGenericTypeDefinition() == typeof(ActiveEntity<,>))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }

        return false;
    }

    private static bool IsActiveEntityTypeCollection(Type type)
    {
        if (type?.IsGenericType != true)
        {
            return false;
        }

        var genericTypeDef = type.GetGenericTypeDefinition();
        if (genericTypeDef != typeof(IEnumerable<>) &&
            genericTypeDef != typeof(ICollection<>) &&
            genericTypeDef != typeof(IList<>) &&
            genericTypeDef != typeof(List<>) &&
            !type.GetInterfaces().Any(i => i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
        {
            return false;
        }

        var elementType = type.GetGenericArguments()[0];
        return IsActiveEntityType(elementType);
    }
}