// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Fluent builder for applying complex changes to entities.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="EntityChangeBuilder{TEntity}"/> class.
/// </remarks>
public class EntityChangeBuilder<TEntity>(TEntity entity)
    where TEntity : IEntity
{
    // Unified storage for all operations in declaration order
    private readonly List<IOrderedOperation> orderedOperations = [];

    // Configuration flags
    private bool registerSingle = true;
    private bool registerNoEvents;

    // Initialized to Success. Since Result is a struct, we avoid null checks and rely on IsFailure state.
    private Result chainConstructionFailure = Result.Success();

    /// <summary>
    /// Specifies whether to replace existing domain events of the same type.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .RegisterEnsureSingle(false)  // Keep all events, don't replace
    ///     .Set(p => p.Status, newStatus)
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> RegisterEnsureSingle(bool replace = true)
    {
        this.registerSingle = replace;
        return this;
    }

    /// <summary>
    /// Specifies that no domain events should be registered when changes are applied.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .RegisterNoEvents()  // Silent update, no events
    ///     .Set(p => p.LastLoginAt, DateTime.UtcNow)
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> RegisterNoEvents(bool noEvents = true)
    {
        this.registerNoEvents = noEvents;
        return this;
    }

    // -------------------------------------------------------------------------
    // 0. Result Transformations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies a transformation to the entity using Result functional extensions at the current position.
    /// Enables use of Result functional extensions (Map, Bind, Tap, Ensure, Filter, etc.).
    /// Can be used standalone or chained after Set/Add/Remove operations.
    /// Multiple Execute calls are executed sequentially at their declaration points.
    /// </summary>
    /// <param name="transformation">Function to transform the Result. Receives Result{TEntity}, returns Result{TEntity}.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Standalone usage - no Set/Add required
    /// return this.Change()
    ///     .Execute(r => r.Tap(e => logger.LogInformation($"Processing {e.Name}")))
    ///     .Apply();
    ///
    /// // Chained with operations
    /// return this.Change()
    ///     .Set(p => p.FirstName, "John")
    ///     .Execute(r => r.Tap(e => Console.WriteLine($"Changed: {e.FirstName}")))
    ///     .Execute(r => r.Ensure(e => e.FirstName.Length > 0, new ValidationError("Name required")))
    ///     .Apply();
    ///
    /// // Using Map to transform
    /// return this.Change()
    ///     .Set(p => p.Age, 25)
    ///     .Execute(r => r.Map(e => { e.UpdatedAt = DateTime.UtcNow; return e; }))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Execute(Func<Result<TEntity>, Result<TEntity>> transformation)
    {
        if (transformation != null)
        {
            this.orderedOperations.Add(new ResultTransformOperationOrdered(transformation));
        }

        return this;
    }

    // -------------------------------------------------------------------------
    // 1. Set Operations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Queues a property change with a direct value.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.FirstName, "John")
    ///     .Set(p => p.Age, 30)
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Set<TValue>(
        Expression<Func<TEntity, TValue>> propertyExpression,
        TValue newValue,
        IEqualityComparer<TValue> comparer = null)
    {
        this.orderedOperations.Add(new SetOperationOrdered<TValue>(propertyExpression, _ => newValue, comparer));
        return this;
    }

    /// <summary>
    /// Queues a property change with a computed value.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.FullName, p => $"{p.FirstName} {p.LastName}")
    ///     .Set(p => p.UpdatedAt, _ => DateTime.UtcNow)
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Set<TValue>(
        Expression<Func<TEntity, TValue>> propertyExpression,
        Func<TEntity, TValue> valueFactory,
        IEqualityComparer<TValue> comparer = null)
    {
        this.orderedOperations.Add(new SetOperationOrdered<TValue>(propertyExpression, valueFactory, comparer));
        return this;
    }

    /// <summary>
    /// Queues a property change using a <see cref="Result{T}"/>.
    /// If the Result is a Failure, the entire transaction will fail when <see cref="Apply"/> is called.
    /// </summary>
    /// <example>
    /// <code>
    /// var emailResult = EmailAddress.Create(emailString);
    /// return this.Change()
    ///     .Set(p => p.Email, emailResult)  // Transaction fails if emailResult is failure
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Set<TValue>(
        Expression<Func<TEntity, TValue>> propertyExpression,
        Result<TValue> result,
        IEqualityComparer<TValue> comparer = null)
    {
        if (result.IsFailure)
        {
            // If we haven't failed yet, capture this failure
            if (this.chainConstructionFailure.IsSuccess)
            {
                // Convert Result<T> failure to non-generic Result failure
                this.chainConstructionFailure = Result.Failure(result.Messages, result.Errors);
            }
            return this;
        }

        return this.Set(propertyExpression, result.Value, comparer);
    }

    /// <summary>
    /// Queues a property change using a function that returns a <see cref="Result{T}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.Email, p => EmailAddress.Create(p.RawEmail))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Set<TValue>(
        Expression<Func<TEntity, TValue>> propertyExpression,
        Func<TEntity, Result<TValue>> valueFactory,
        IEqualityComparer<TValue> comparer = null)
    {
        this.orderedOperations.Add(new ResultSetOperationOrdered<TValue>(propertyExpression, valueFactory, comparer));
        return this;
    }

    // -------------------------------------------------------------------------
    // 2. Collection Operations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Queues an operation to add an item to a collection property.
    /// </summary>
    /// <example>
    /// <code>
    /// var address = new Address("123 Main St", "New York");
    /// return this.Change()
    ///     .Add(p => p.Addresses, address)
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Add<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TItem item,
        IEqualityComparer<TItem> comparer = null)
    {
        this.orderedOperations.Add(new CollectionAddOperationOrdered<TItem>(collectionExpression, item, comparer));
        return this;
    }

    /// <summary>
    /// Queues an operation to add an item to a collection property using a <see cref="Result{T}"/>.
    /// If the Result is a Failure, the entire transaction will fail when <see cref="Apply"/> is called.
    /// </summary>
    /// <example>
    /// <code>
    /// var addressResult = Address.Create("123 Main St");
    /// return this.Change()
    ///     .Add(e => e.Addresses, addressResult)  // Only add if addressResult is success
    ///     .Register(e => new CustomerUpdatedDomainEvent(e))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Add<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Result<TItem> result,
        IEqualityComparer<TItem> comparer = null)
    {
        if (result.IsFailure)
        {
            // If we haven't failed yet, capture this failure
            if (this.chainConstructionFailure.IsSuccess)
            {
                // Convert Result<T> failure to non-generic Result failure
                this.chainConstructionFailure = Result.Failure(result.Messages, result.Errors);
            }
            return this;
        }

        return this.Add(collectionExpression, result.Value, comparer);
    }

    /// <summary>
    /// Queues an operation to add an item to a collection property using a function that returns a <see cref="Result{T}"/>.
    /// If the computed Result is a Failure, the transaction stops and returns that failure.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Add(e => e.Addresses, agg => Address.Create(dto.Street))
    ///     .Register(e => new CustomerUpdatedDomainEvent(e))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Add<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TEntity, Result<TItem>> itemFactory,
        IEqualityComparer<TItem> comparer = null)
    {
        this.orderedOperations.Add(new ResultCollectionAddOperationOrdered<TItem>(collectionExpression, itemFactory, comparer));
        return this;
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection property.
    /// Returns a failure with <see cref="NotFoundError"/> if the item is not found in the collection.
    /// </summary>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="item">The item to remove.</param>
    /// <param name="comparer">Optional equality comparer for item comparison.</param>
    /// <param name="errorMessage">Optional custom error message when item is not found. If null, uses default message.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Remove(p => p.Addresses, oldAddress, errorMessage: "Address not found")
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Remove<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TItem item,
        IEqualityComparer<TItem> comparer = null,
        string errorMessage = null,
        IResultError error = null)
    {
        this.orderedOperations.Add(new CollectionRemoveOperationOrdered<TItem>(collectionExpression, item, comparer, errorMessage, error));
        return this;
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection property using a <see cref="Result{T}"/>.
    /// If the Result is a Failure, the entire transaction will fail when <see cref="Apply"/> is called.
    /// Returns a failure with <see cref="NotFoundError"/> if the item is not found in the collection.
    /// </summary>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="result">Result containing the item to remove.</param>
    /// <param name="comparer">Optional equality comparer for item comparison.</param>
    /// <param name="errorMessage">Optional custom error message when item is not found. If null, uses default message.</param>
    /// <example>
    /// <code>
    /// var addressResult = FindAddressById(addressId);
    /// return this.Change()
    ///     .Remove(p => p.Addresses, addressResult, errorMessage: "Address not found")
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Remove<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Result<TItem> result,
        IEqualityComparer<TItem> comparer = null,
        string errorMessage = null,
        IResultError error = null)
    {
        if (result.IsFailure)
        {
            if (this.chainConstructionFailure.IsSuccess)
            {
                this.chainConstructionFailure = Result.Failure(result.Messages, result.Errors);
            }
            return this;
        }

        return this.Remove(collectionExpression, result.Value, comparer, errorMessage, error);
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection property using a function that returns a <see cref="Result{T}"/>.
    /// If the computed Result is a Failure, the transaction stops and returns that failure.
    /// Returns a failure with <see cref="NotFoundError"/> if the item is not found in the collection.
    /// </summary>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="itemFactory">Function that returns a Result containing the item to remove.</param>
    /// <param name="comparer">Optional equality comparer for item comparison.</param>
    /// <param name="errorMessage">Optional custom error message when item is not found. If null, uses default message.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Remove(p => p.Addresses, p => p.FindAddressById(addressId), errorMessage: "Address not found")
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> RemoveBy<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TEntity, Result<TItem>> itemFactory,
        IEqualityComparer<TItem> comparer = null,
        string errorMessage = null,
        IResultError error = null)
    {
        this.orderedOperations.Add(
            new ResultCollectionRemoveOperationOrdered<TItem>(collectionExpression, itemFactory, comparer, errorMessage, error));
        return this;
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection by its ID.
    /// The item type must implement <see cref="IEntity{TId}"/>.
    /// Returns a failure with <see cref="NotFoundError"/> if no item with the specified ID is found.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection. Must implement IEntity{TId}.</typeparam>
    /// <typeparam name="TId">The type of the entity ID.</typeparam>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="id">The ID of the item to remove.</param>
    /// <param name="comparer">Optional equality comparer for ID comparison.</param>
    /// <param name="errorMessage">Optional custom error message when item is not found. If null, uses default message.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .RemoveById(e => e.Addresses, addressId, errorMessage: "Address not found")
    ///     .Register(e => new CustomerUpdatedDomainEvent(e))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> RemoveById<TItem, TId>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TId id,
        IEqualityComparer<TId> comparer = null,
        string errorMessage = null,
        IResultError error = null)
        where TItem : IEntity<TId>
    {
        this.orderedOperations.Add(new CollectionRemoveByIdOperationOrdered<TItem, TId>(collectionExpression, id, comparer, errorMessage, error));
        return this;
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection by its ID using a <see cref="Result{TId}"/>.
    /// The item type must implement <see cref="IEntity{TId}"/>.
    /// If the Result is a Failure, the entire transaction will fail when <see cref="Apply"/> is called.
    /// Returns a failure with <see cref="NotFoundError"/> if no item with the specified ID is found.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection. Must implement IEntity{TId}.</typeparam>
    /// <typeparam name="TId">The type of the entity ID.</typeparam>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="result">Result containing the ID of the item to remove.</param>
    /// <param name="comparer">Optional equality comparer for ID comparison.</param>
    /// <param name="errorMessage">Optional custom error message when item is not found. If null, uses default message.</param>
    /// <example>
    /// <code>
    /// var idResult = GetAddressId();
    /// return this.Change()
    ///     .RemoveById(e => e.Addresses, idResult, errorMessage: "Address not found")
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> RemoveById<TItem, TId>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Result<TId> result,
        IEqualityComparer<TId> comparer = null,
        string errorMessage = null,
        IResultError error = null)
        where TItem : IEntity<TId>
    {
        if (result.IsFailure)
        {
            if (this.chainConstructionFailure.IsSuccess)
            {
                this.chainConstructionFailure = Result.Failure(result.Messages, result.Errors);
            }
            return this;
        }

        return this.RemoveById<TItem, TId>(collectionExpression, result.Value, comparer, errorMessage, error);
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection by its ID using a function that returns a <see cref="Result{TId}"/>.
    /// The item type must implement <see cref="IEntity{TId}"/>.
    /// If the computed Result is a Failure, the transaction stops and returns that failure.
    /// Returns a failure with <see cref="NotFoundError"/> if no item with the specified ID is found.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection. Must implement IEntity{TId}.</typeparam>
    /// <typeparam name="TId">The type of the entity ID.</typeparam>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="idFactory">Function that returns a Result containing the ID of the item to remove.</param>
    /// <param name="comparer">Optional equality comparer for ID comparison.</param>
    /// <param name="errorMessage">Optional custom error message when item is not found. If null, uses default message.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .RemoveById&lt;Address, AddressId&gt;(e => e.Addresses, e => e.GetPrimaryAddressId(), errorMessage: "Address not found")
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> RemoveById<TItem, TId>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TEntity, Result<TId>> idFactory,
        IEqualityComparer<TId> comparer = null,
        string errorMessage = null,
        IResultError error = null)
        where TItem : IEntity<TId>
    {
        this.orderedOperations.Add(
            new ResultCollectionRemoveByIdOperationOrdered<TItem, TId>(collectionExpression, idFactory, comparer, errorMessage, error));
        return this;
    }

    /// <summary>
    /// Queues an operation to clear all items from a collection property.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Clear(p => p.Addresses)
    ///     .Clear(p => p.Tags)
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Clear<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression)
    {
        this.orderedOperations.Add(new ClearCollectionOperationOrdered<TItem>(collectionExpression));
        return this;
    }

    // -------------------------------------------------------------------------
    // 2b. Collection Item Operations (Set operations on items)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies an action to all items in a collection.
    /// If collection is empty, returns success with hasChanged = false.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="action">Action to apply to each item in the collection.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(e => e.Addresses, a => a.ClearPrimary())
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Set<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Action<TItem> action)
    {
        this.orderedOperations.Add(
            new CollectionSetAllOperationOrdered<TItem>(collectionExpression, action));
        return this;
    }

    /// <summary>
    /// Applies a Result-returning action to all items in a collection.
    /// If any action fails, the transaction stops and returns that failure.
    /// If collection is empty, returns success with hasChanged = false.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="action">Result-returning action to apply to each item.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(e => e.Addresses, a => a.Validate())
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Set<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TItem, Result> action)
    {
        this.orderedOperations.Add(
            new ResultCollectionSetAllOperationOrdered<TItem>(collectionExpression, action));
        return this;
    }

    /// <summary>
    /// Applies an action to filtered items in a collection.
    /// If no items match the filter, returns success with hasChanged = false.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="filter">Predicate to filter items.</param>
    /// <param name="action">Action to apply to each matching item.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(e => e.Addresses, a => a.IsActive, a => a.Renew())
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> SetBy<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TItem, bool> filter,
        Action<TItem> action)
    {
        this.orderedOperations.Add(new CollectionSetFilteredOperationOrdered<TItem>(collectionExpression, filter, action));
        return this;
    }

    /// <summary>
    /// Applies a Result-returning action to filtered items in a collection.
    /// If any action fails, the transaction stops and returns that failure.
    /// If no items match the filter, returns success with hasChanged = false.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="filter">Predicate to filter items.</param>
    /// <param name="action">Result-returning action to apply to each matching item.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(e => e.Subscriptions, s => s.IsExpired, s => s.Renew())
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> SetBy<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TItem, bool> filter,
        Func<TItem, Result> action)
    {
        this.orderedOperations.Add(new ResultCollectionSetFilteredOperationOrdered<TItem>(collectionExpression, filter, action));
        return this;
    }

    /// <summary>
    /// Applies an action to a single item in a collection identified by its ID.
    /// Returns a failure with <see cref="NotFoundError"/> if no item with the specified ID is found.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection. Must implement IEntity{TId}.</typeparam>
    /// <typeparam name="TId">The type of the entity ID.</typeparam>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="id">The ID of the item to operate on.</param>
    /// <param name="action">Action to apply to the matching item.</param>
    /// <param name="errorMessage">Optional custom error message when item is not found.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(e => e.Addresses, addressId, a => a.SetPrimary(), "Address not found")
    ///     .Register(e => new CustomerUpdatedDomainEvent(e))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> SetById<TItem, TId>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TId id,
        Action<TItem> action,
        string errorMessage = null,
        IResultError error = null)
        where TItem : IEntity<TId>
    {
        this.orderedOperations.Add(new CollectionSetByIdOperationOrdered<TItem, TId>(collectionExpression, id, action, errorMessage, error));
        return this;
    }

    /// <summary>
    /// Applies a Result-returning action to a single item in a collection identified by its ID.
    /// Returns a failure with <see cref="NotFoundError"/> if no item with the specified ID is found.
    /// If the action fails, returns that failure.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection. Must implement IEntity{TId}.</typeparam>
    /// <typeparam name="TId">The type of the entity ID.</typeparam>
    /// <param name="collectionExpression">Expression selecting the collection property.</param>
    /// <param name="id">The ID of the item to operate on.</param>
    /// <param name="action">Result-returning action to apply to the matching item.</param>
    /// <param name="errorMessage">Optional custom error message when item is not found.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(e => e.Addresses, addressId, a => a.Activate(), "Address not found")
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> SetById<TItem, TId>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TId id,
        Func<TItem, Result> action,
        string errorMessage = null,
        IResultError error = null)
        where TItem : IEntity<TId>
    {
        this.orderedOperations.Add(new ResultCollectionSetByIdOperationOrdered<TItem, TId>(collectionExpression, id, action, errorMessage, error));
        return this;
    }

    // -------------------------------------------------------------------------
    // 3. Logic & Guards
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a circuit breaker condition at the current position in the operation chain.
    /// If the predicate returns false when executed, ALL remaining operations after this point are cancelled.
    /// Operations before this When() execute normally.
    /// </summary>
    /// <param name="predicate">The condition to evaluate against the entity.</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.Name, "John")        // Always executes
    ///     .When(_ => age >= 18)            // Circuit breaker
    ///     .Set(p => p.Status, Adult)       // Only if age >= 18
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> When(Func<TEntity, bool> predicate)
    {
        this.orderedOperations.Add(new WhenOperation(predicate));
        return this;
    }

    /// <summary>
    /// Adds a pre-condition check at the current position. If the predicate returns false, the transaction aborts.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Ensure(p => p.Age >= 18, "Must be an adult")
    ///     .Set(p => p.CanVote, true)
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Ensure(Func<TEntity, bool> predicate, string errorMessage)
    {
        this.orderedOperations.Add(new EnsureOperationOrdered(predicate, errorMessage, null));
        return this;
    }

    /// <summary>
    /// Adds a pre-condition check at the current position. If the predicate returns false, the transaction aborts.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Ensure(p => p.Age >= 18, new ValidationError("Must be an adult"))
    ///     .Set(p => p.CanVote, true)
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Ensure(Func<TEntity, bool> predicate, IResultError error)
    {
        this.orderedOperations.Add(new EnsureOperationOrdered(predicate, null, error));
        return this;
    }

    /// <summary>
    /// Executes an arbitrary action on the entity at the current position.
    /// If the action throws an unhandled exception, the transaction stops and returns a failure.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Execute(p => p.addresses.Clear())  // If throws, chain stops with failure
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Execute(Action<TEntity> action)
    {
        this.orderedOperations.Add(new ExecuteOperationOrdered(action));
        return this;
    }

    /// <summary>
    /// Applies changes by calling a method that returns a <see cref="Result"/> on the entity.
    /// If the Result is a Failure, the transaction stops and returns that failure.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.ChangeAge(age))  // If ChangeAge returns failure, chain stops
    ///     .Set(p => p.ChangeEmail(email))  // Only runs if previous Set succeeded
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Set(Func<TEntity, Result> func)
    {
        this.orderedOperations.Add(new ResultExecuteOperationOrdered(func));
        return this;
    }

    /// <summary>
    /// Applies changes by calling a method that returns a <see cref="Result{TEntity}"/> on the entity.
    /// If the Result is a Failure, the transaction stops and returns that failure.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.ChangeName(first, last))  // If ChangeName returns failure, chain stops
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Set(Func<TEntity, Result<TEntity>> func)
    {
        this.orderedOperations.Add(new ResultEntityExecuteOperationOrdered(func));
        return this;
    }

    // -------------------------------------------------------------------------
    // 4. Validation & Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a validation check at the current position in the operation chain.
    /// If the predicate returns false, the transaction aborts with an error.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.FirstName, firstName)
    ///     .Set(p => p.LastName, lastName)
    ///     .Check(p => !string.IsNullOrEmpty(p.FirstName), "First name is required")
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Check(Func<TEntity, bool> predicate, string errorMessage)
    {
        this.orderedOperations.Add(new CheckOperationOrdered(predicate, errorMessage, null));
        return this;
    }

    /// <summary>
    /// Adds a validation check at the current position in the operation chain.
    /// If the predicate returns false, the transaction aborts with an error.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.FirstName, firstName)
    ///     .Set(p => p.LastName, lastName)
    ///     .Check(p => !string.IsNullOrEmpty(p.FirstName), new ValidationError("First name is required"))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Check(Func<TEntity, bool> predicate, IResultError error)
    {
        this.orderedOperations.Add(new CheckOperationOrdered(predicate, null, error));
        return this;
    }

    /// <summary>
    /// Queues a custom domain event to be registered at Apply() end if changes occur.
    /// Note: This will throw an InvalidOperationException at runtime if the entity does not implement IAggregateRoot.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.Email, newEmail)
    ///     .Register((p, ctx) => new EmailChangedEvent(
    ///         ctx.GetOldValue&lt;string&gt;(nameof(Email)),
    ///         p.Email))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Register<TEvent>(Func<TEntity, EntityChangeContext, TEvent> eventFactory) //1
        where TEvent : IDomainEvent
    {
        this.orderedOperations.Add(new RegisterOperationOrdered((agg, ctx) => eventFactory(agg, ctx)));
        return this;
    }

    /// <summary>
    /// Queues a custom domain event to be registered at Apply() end if changes occur.
    /// Note: This will throw an InvalidOperationException at runtime if the entity does not implement IAggregateRoot.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.FirstName, firstName)
    ///     .Set(p => p.LastName, lastName)
    ///     .Register(p => new CustomerNameChangedEvent(p.Id))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Register<TEvent>(Func<TEntity, TEvent> eventFactory) //2
        where TEvent : IDomainEvent
    {
        this.orderedOperations.Add(new RegisterOperationOrdered((agg, _) => eventFactory(agg)));
        return this;
    }

    /// <summary>
    /// Queues a custom domain event to be registered on the specified target at Apply() end if changes occur.
    /// Note: This will throw an InvalidOperationException at runtime if the entity does not implement IAggregateRoot.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.FirstName, firstName)
    ///     .Set(p => p.LastName, lastName)
    ///     .Register(target, p => new CustomerNameChangedEvent(p.Id))
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> Register<TEvent>(Func<TEntity, TEvent> eventFactory, IAggregateRoot target = null) //3
        where TEvent : IDomainEvent
    {
        this.orderedOperations.Add(new RegisterOperationOrdered((agg, _) => eventFactory(agg), target));
        return this;
    }

    /// <summary>
    /// Queues a custom action to be executed at Apply() end if changes occur.
    /// </summary>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(p => p.Status, newStatus)
    ///     .OnChanged(e => e.AuditState.SetUpdated())
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> OnChanged(Action<TEntity> action)
    {
        if (action != null)
        {
            this.orderedOperations.Add(new OnChangedOperationOrdered(action));
        }
        return this;
    }

    // -------------------------------------------------------------------------
    // 5. Apply
    // -------------------------------------------------------------------------

    /// <summary>
    /// Executes the change transaction in declaration order.
    /// <para>
    /// Steps:
    /// 1. Checks for fast-fail errors from Result-based chaining operations.
    /// 2. Executes all queued operations sequentially in declaration order.
    /// 3. When operations act as circuit breakers - if condition fails, remaining operations are cancelled.
    /// 4. Registers domain events at the end if changes were made and no cancellation occurred (only for IAggregateRoot instances).
    /// </para>
    /// </summary>
    public Result<TEntity> Apply()
    {
        // 1. Check for fast-fail errors captured during chaining
        if (this.chainConstructionFailure.IsFailure)
        {
            return Result<TEntity>.Failure(entity)
                .WithMessages(this.chainConstructionFailure.Messages)
                .WithErrors(this.chainConstructionFailure.Errors);
        }

        var context = new EntityChangeOrderedExecutionContext<TEntity>();

        // 2. Execute all operations in declaration order
        foreach (var operation in this.orderedOperations)
        {
            var opResult = operation.Execute(entity, context);

            // Handle cancellation (When circuit breaker triggered)
            if (opResult.IsCancelled)
            {
                // All operations before When executed, skip remaining operations
                // Register queued events since changes before cancellation should be preserved
                this.RegisterQueuedEvents(entity, context);
                return Result<TEntity>.Success(entity);
            }

            // Handle failure
            if (opResult.IsFailure)
            {
                return Result<TEntity>.Failure(entity)
                    .WithErrors(opResult.Errors)
                    .WithMessages(opResult.Messages);
            }

            // Track changes
            if (opResult.HasChanged)
            {
                context.ChangesMade = true;
            }
        }

        // 3. Register queued events if changes were made (only for IAggregateRoot instances)
        this.RegisterQueuedEvents(entity, context);

        // 4. Execute queued OnChanged actions if changes were made
        if (context.ChangesMade)
        {
            foreach (var action in context.QueuedOnChangedActions)
            {
                try
                {
                    action(entity);
                }
                catch (Exception ex)
                {
                    return Result<TEntity>.Failure(entity).WithError(new ExceptionError(ex));
                }
            }
        }

        return Result<TEntity>.Success(entity);
    }

    /// <summary>
    /// Registers all queued events if changes were made.
    /// Events are registered on their specified target aggregate, or on the entity itself if no target is specified.
    /// </summary>
    private void RegisterQueuedEvents(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
    {
        if (!context.ChangesMade || this.registerNoEvents)
        {
            return;
        }

        // Check if any queued events lack a target and entity is not an aggregate root
        var entityAsAggregateRoot = entity as IAggregateRoot;
        var hasEventsWithoutTarget = context.QueuedEvents.Any(q => q.Target == null);

        if (hasEventsWithoutTarget && entityAsAggregateRoot == null)
        {
            throw new InvalidOperationException(
                $"Cannot register domain events on entity of type '{typeof(TEntity).Name}' because it does not implement IAggregateRoot. " +
                "Either specify a target aggregate root using the Register overload, or use an aggregate root as the entity.");
        }

        // Register all queued events on their respective targets
        foreach (var (eventFactory, target) in context.QueuedEvents)
        {
            var evt = eventFactory();
            var targetAggregate = target ?? entityAsAggregateRoot;

            // This should not happen due to the check above, but being defensive
            if (targetAggregate == null)
            {
                throw new InvalidOperationException(
                    $"Cannot register domain event '{evt.GetType().Name}'. No target aggregate specified and entity '{typeof(TEntity).Name}' does not implement IAggregateRoot.");
            }

            targetAggregate.DomainEvents.Register(evt, this.registerSingle);
        }

        // If no events were explicitly registered, register default EntityUpdatedDomainEvent on the entity (if it's an aggregate root)
        if (context.QueuedEvents.Count == 0 && entityAsAggregateRoot != null)
        {
            var eventType = typeof(EntityUpdatedDomainEvent<>).MakeGenericType(entity.GetType());
            var domainEvent = (IDomainEvent)Activator.CreateInstance(eventType, entity);
            entityAsAggregateRoot.DomainEvents.Register(domainEvent, this.registerSingle);
        }
    }

    // -------------------------------------------------------------------------
    // Internal Infrastructure
    // -------------------------------------------------------------------------

    /// <summary>
    /// Interface for operations that execute in declaration order.
    /// </summary>
    private interface IOrderedOperation
    {
        /// <summary>
        /// Executes the operation on the entity using the provided context.
        /// </summary>
        /// <param name="entity">The entity to operate on.</param>
        /// <param name="context">The execution context tracking changes and events.</param>
        /// <returns>The result of the operation execution.</returns>
        EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context);
    }

    /// <summary>
    /// Property accessor with compiled getters and setters for performance.
    /// </summary>
    private class PropertyAccessor<TValue>
    {
        public PropertyInfo PropertyInfo { get; }
        private readonly Func<TEntity, TValue> getter;
        private readonly Action<TEntity, TValue> setter;
        private static readonly ConcurrentDictionary<string, (Func<TEntity, TValue>, Action<TEntity, TValue>)> Cache = [];

        public PropertyAccessor(Expression<Func<TEntity, TValue>> propertyExpression)
        {
            // Extract PropertyInfo from expression
            var memberExpression = propertyExpression.Body as MemberExpression ?? throw new ArgumentException("Expression must be a property access");
            // Handle both properties and fields (backing fields for properties)
            this.PropertyInfo = memberExpression.Member as PropertyInfo ?? throw new ArgumentException("Expression must reference a property");
            var key = $"{typeof(TEntity).FullName}.{this.PropertyInfo.Name}";

            var accessors = Cache.GetOrAdd(key, _ =>
            {
                var instanceParam = Expression.Parameter(typeof(TEntity), "instance");
                var propertyAccess = Expression.Property(instanceParam, this.PropertyInfo);
                var compiledGetter = Expression.Lambda<Func<TEntity, TValue>>(propertyAccess, instanceParam).Compile();

                var valueParam = Expression.Parameter(typeof(TValue), "value");
                var assignExpression = Expression.Assign(Expression.Property(instanceParam, this.PropertyInfo), valueParam);
                var compiledSetter = Expression.Lambda<Action<TEntity, TValue>>(assignExpression, instanceParam, valueParam).Compile();

                return (compiledGetter, compiledSetter);
            });

            this.getter = accessors.Item1;
            this.setter = accessors.Item2;
        }

        public TValue GetValue(TEntity instance) => this.getter(instance);
        public void SetValue(TEntity instance, TValue value) => this.setter(instance, value);
    }

    // -------------------------------------------------------------------------
    // NEW ORDERED OPERATION IMPLEMENTATIONS
    // -------------------------------------------------------------------------

    /// <summary>
    /// When operation acts as a circuit breaker - cancels all remaining operations if predicate fails.
    /// </summary>
    private class WhenOperation(Func<TEntity, bool> predicate) : IOrderedOperation
    {
        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            // If predicate passes, continue execution
            if (predicate(entity))
            {
                return EntityChangeOperationExecutionResult.Success();
            }

            // If predicate fails, signal cancellation (circuit breaker)
            return EntityChangeOperationExecutionResult.Cancelled();
        }
    }

    /// <summary>
    /// Set operation for ordered execution - sets a property value with change tracking.
    /// </summary>
    private class SetOperationOrdered<TValue>(
        Expression<Func<TEntity, TValue>> propertyExpression,
        Func<TEntity, TValue> valueFactory,
        IEqualityComparer<TValue> comparer) : IOrderedOperation
    {
        private readonly PropertyAccessor<TValue> accessor = new(propertyExpression);
        private readonly IEqualityComparer<TValue> comparer = comparer ?? EqualityComparer<TValue>.Default;

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var currentValue = this.accessor.GetValue(entity);
            var newValue = valueFactory(entity);

            if (this.comparer.Equals(currentValue, newValue))
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            this.accessor.SetValue(entity, newValue);
            context.RecordChange(this.accessor.PropertyInfo.Name, currentValue);
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Set operation with Result-returning factory for ordered execution.
    /// </summary>
    private class ResultSetOperationOrdered<TValue>(
        Expression<Func<TEntity, TValue>> propertyExpression,
        Func<TEntity, Result<TValue>> valueFactory,
        IEqualityComparer<TValue> comparer) : IOrderedOperation
    {
        private readonly PropertyAccessor<TValue> accessor = new(propertyExpression);
        private readonly IEqualityComparer<TValue> comparer = comparer ?? EqualityComparer<TValue>.Default;

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var result = valueFactory(entity);
            if (result.IsFailure)
            {
                return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
            }

            var currentValue = this.accessor.GetValue(entity);
            if (this.comparer.Equals(currentValue, result.Value))
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            this.accessor.SetValue(entity, result.Value);
            context.RecordChange(this.accessor.PropertyInfo.Name, currentValue);
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Check operation for ordered execution - validates condition at declaration point.
    /// </summary>
    private class CheckOperationOrdered(Func<TEntity, bool> predicate, string errorMessage = null, IResultError error = null) : IOrderedOperation
    {
        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            if (!predicate(entity))
            {
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new ChangeError(errorMessage)]);
            }

            return EntityChangeOperationExecutionResult.Success();
        }
    }

    /// <summary>
    /// Ensure operation for ordered execution - validates pre-condition.
    /// </summary>
    private class EnsureOperationOrdered(Func<TEntity, bool> predicate, string errorMessage = null, IResultError error = null) : IOrderedOperation
    {
        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            if (!predicate(entity))
            {
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new ChangeError(errorMessage)]);
            }

            return EntityChangeOperationExecutionResult.Success();
        }
    }

    /// <summary>
    /// Execute operation for void actions in ordered execution.
    /// </summary>
    private class ExecuteOperationOrdered(Action<TEntity> action) : IOrderedOperation
    {
        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            try
            {
                action(entity);
                context.RecordChange("Execute", null);
                return EntityChangeOperationExecutionResult.Success(hasChanged: true);
            }
            catch (Exception ex)
            {
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [Result.Settings.ExceptionErrorFactory(ex)],
                    messages: [ex.Message]);
            }
        }
    }

    /// <summary>
    /// Execute operation for Result-returning functions in ordered execution.
    /// </summary>
    private class ResultExecuteOperationOrdered(Func<TEntity, Result> func) : IOrderedOperation
    {
        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var result = func(entity);
            if (result.IsFailure)
            {
                return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
            }

            context.RecordChange("Execute", null);
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Execute operation for Result<TEntity>-returning functions in ordered execution.
    /// </summary>
    private class ResultEntityExecuteOperationOrdered(Func<TEntity, Result<TEntity>> func) : IOrderedOperation
    {
        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var result = func(entity);
            if (result.IsFailure)
            {
                return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
            }

            context.RecordChange("Execute", null);
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Execute operation for Result<TEntity> transformations in ordered execution.
    /// </summary>
    private class ResultTransformOperationOrdered(Func<Result<TEntity>, Result<TEntity>> transformation) : IOrderedOperation
    {
        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var entityResult = Result<TEntity>.Success(entity);
            var result = transformation(entityResult);

            if (result.IsFailure)
            {
                return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
            }

            // Note: transformation might have changed entity state
            // We mark as changed to be safe
            context.ChangesMade = true;
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Register operation for ordered execution - queues event factory for later registration.
    /// </summary>
    private class RegisterOperationOrdered(Func<TEntity, EntityChangeContext, IDomainEvent> eventFactory, IAggregateRoot target = null) : IOrderedOperation
    {
        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            // Queue the event factory to be registered at Apply() end, along with the optional target aggregate
            context.QueueEvent(() =>
            {
                // Create EntityChangeContext from OrderedExecutionContext
                var changeContext = new EntityChangeContext();
                foreach (var kvp in context.OldValues)
                {
                    changeContext.RecordChange(kvp.Key, kvp.Value, null); // We don't track new values separately
                }

                return eventFactory(entity, changeContext);
            }, target);

            return EntityChangeOperationExecutionResult.Success();
        }
    }

    /// <summary>
    /// OnChanged operation for ordered execution - queues action for later execution.
    /// </summary>
    private class OnChangedOperationOrdered(Action<TEntity> action) : IOrderedOperation
    {
        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            context.QueuedOnChangedActions.Add(action);
            return EntityChangeOperationExecutionResult.Success();
        }
    }

    /// <summary>
    /// Collection add operation for ordered execution.
    /// </summary>
    private class CollectionAddOperationOrdered<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TItem item,
        IEqualityComparer<TItem> comparer) : IOrderedOperation
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly IEqualityComparer<TItem> comparer = comparer ?? EqualityComparer<TItem>.Default;
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            var exists = this.comparer == EqualityComparer<TItem>.Default
                ? collection.Contains(item)
                : collection.Any(x => this.comparer.Equals(x, item));

            if (exists)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            collection.Add(item);
            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection add operation with Result-returning factory for ordered execution.
    /// </summary>
    private class ResultCollectionAddOperationOrdered<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TEntity, Result<TItem>> itemFactory,
        IEqualityComparer<TItem> comparer) : IOrderedOperation
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly IEqualityComparer<TItem> comparer = comparer ?? EqualityComparer<TItem>.Default;
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var result = itemFactory(entity);
            if (result.IsFailure)
            {
                return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
            }

            var item = result.Value;
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            var exists = this.comparer == EqualityComparer<TItem>.Default
                ? collection.Contains(item)
                : collection.Any(x => this.comparer.Equals(x, item));

            if (exists)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            collection.Add(item);
            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection remove operation for ordered execution.
    /// Returns NotFoundError if item is not in the collection.
    /// </summary>
    private class CollectionRemoveOperationOrdered<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TItem item,
        IEqualityComparer<TItem> comparer,
        string errorMessage = null,
        IResultError error = null) : IOrderedOperation
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly IEqualityComparer<TItem> comparer = comparer ?? EqualityComparer<TItem>.Default;
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                var message = errorMessage ?? $"Cannot remove item from null collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            var exists = this.comparer == EqualityComparer<TItem>.Default
                ? collection.Contains(item)
                : collection.Any(x => this.comparer.Equals(x, item));

            if (!exists)
            {
                var message = errorMessage ?? $"Item not found in collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            if (this.comparer != EqualityComparer<TItem>.Default)
            {
                var itemToRemove = collection.First(x => this.comparer.Equals(x, item));
                collection.Remove(itemToRemove);
            }
            else
            {
                collection.Remove(item);
            }

            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection remove operation with Result-returning factory for ordered execution.
    /// Returns NotFoundError if item is not in the collection.
    /// </summary>
    private class ResultCollectionRemoveOperationOrdered<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TEntity, Result<TItem>> itemFactory,
        IEqualityComparer<TItem> comparer,
        string errorMessage = null,
        IResultError error = null) : IOrderedOperation
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly IEqualityComparer<TItem> comparer = comparer ?? EqualityComparer<TItem>.Default;
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var result = itemFactory(entity);
            if (result.IsFailure)
            {
                return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
            }

            var item = result.Value;
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                var message = errorMessage ?? $"Cannot remove item from null collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            var exists = this.comparer == EqualityComparer<TItem>.Default
                ? collection.Contains(item)
                : collection.Any(x => this.comparer.Equals(x, item));

            if (!exists)
            {
                var message = errorMessage ?? $"Item not found in collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            if (this.comparer != EqualityComparer<TItem>.Default)
            {
                var itemToRemove = collection.First(x => this.comparer.Equals(x, item));
                collection.Remove(itemToRemove);
            }
            else
            {
                collection.Remove(item);
            }

            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection remove by ID operation for ordered execution.
    /// Returns NotFoundError if no item with the specified ID is found.
    /// </summary>
    private class CollectionRemoveByIdOperationOrdered<TItem, TId>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TId id,
        IEqualityComparer<TId> comparer,
        string errorMessage = null,
        IResultError error = null) : IOrderedOperation
        where TItem : IEntity<TId>
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly IEqualityComparer<TId> comparer = comparer ?? EqualityComparer<TId>.Default;
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                var message = errorMessage ?? $"Cannot remove item from null collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            var itemToRemove = collection.FirstOrDefault(x => this.comparer.Equals(x.Id, id));
            if (itemToRemove == null)
            {
                var message = errorMessage ?? $"Item with ID '{id}' not found in collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            collection.Remove(itemToRemove);
            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection remove by ID operation with Result-returning factory for ordered execution.
    /// Returns NotFoundError if no item with the specified ID is found.
    /// </summary>
    private class ResultCollectionRemoveByIdOperationOrdered<TItem, TId>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TEntity, Result<TId>> idFactory,
        IEqualityComparer<TId> comparer,
        string errorMessage,
        IResultError error = null) : IOrderedOperation
        where TItem : IEntity<TId>
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly IEqualityComparer<TId> comparer = comparer ?? EqualityComparer<TId>.Default;
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var result = idFactory(entity);
            if (result.IsFailure)
            {
                return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
            }

            var id = result.Value;
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                var message = errorMessage ?? $"Cannot remove item from null collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            var itemToRemove = collection.FirstOrDefault(x => this.comparer.Equals(x.Id, id));
            if (itemToRemove == null)
            {
                var message = errorMessage ?? $"Item with ID '{id}' not found in collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            collection.Remove(itemToRemove);
            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection clear operation for ordered execution.
    /// </summary>
    private class ClearCollectionOperationOrdered<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression) : IOrderedOperation
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null || collection.Count == 0)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            collection.Clear();
            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection set all operation - applies action to all items.
    /// </summary>
    private class CollectionSetAllOperationOrdered<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Action<TItem> action) : IOrderedOperation
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null || collection.Count == 0)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            foreach (var item in collection)
            {
                action(item);
            }

            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection set all operation with Result-returning action.
    /// </summary>
    private class ResultCollectionSetAllOperationOrdered<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TItem, Result> action) : IOrderedOperation
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null || collection.Count == 0)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            foreach (var item in collection)
            {
                var result = action(item);
                if (result.IsFailure)
                {
                    return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
                }
            }

            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection set filtered operation - applies action to filtered items.
    /// </summary>
    private class CollectionSetFilteredOperationOrdered<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TItem, bool> filter,
        Action<TItem> action) : IOrderedOperation
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            var matchingItems = collection.Where(filter).ToList();
            if (matchingItems.Count == 0)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            foreach (var item in matchingItems)
            {
                action(item);
            }

            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection set filtered operation with Result-returning action.
    /// </summary>
    private class ResultCollectionSetFilteredOperationOrdered<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TItem, bool> filter,
        Func<TItem, Result> action) : IOrderedOperation
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly string propertyName = GetPropertyName(collectionExpression);

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            var matchingItems = collection.Where(filter).ToList();
            if (matchingItems.Count == 0)
            {
                return EntityChangeOperationExecutionResult.Success(hasChanged: false);
            }

            foreach (var item in matchingItems)
            {
                var result = action(item);
                if (result.IsFailure)
                {
                    return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
                }
            }

            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection set by ID operation - applies action to item by ID.
    /// Returns NotFoundError if item not found.
    /// </summary>
    private class CollectionSetByIdOperationOrdered<TItem, TId>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TId id,
        Action<TItem> action,
        string errorMessage = null,
        IResultError error = null) : IOrderedOperation
        where TItem : IEntity<TId>
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly string propertyName = GetPropertyName(collectionExpression);
        private readonly IEqualityComparer<TId> comparer = EqualityComparer<TId>.Default;

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                var message = errorMessage ?? $"Cannot operate on null collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            var item = collection.FirstOrDefault(x => this.comparer.Equals(x.Id, id));
            if (item == null)
            {
                var message = errorMessage ?? $"Item with ID '{id}' not found in collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            action(item);
            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }

    /// <summary>
    /// Collection set by ID operation with Result-returning action.
    /// Returns NotFoundError if item not found.
    /// </summary>
    private class ResultCollectionSetByIdOperationOrdered<TItem, TId>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TId id,
        Func<TItem, Result> action,
        string errorMessage = null,
        IResultError error = null) : IOrderedOperation
        where TItem : IEntity<TId>
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter = collectionExpression.Compile();
        private readonly string propertyName = GetPropertyName(collectionExpression);
        private readonly IEqualityComparer<TId> comparer = EqualityComparer<TId>.Default;

        private static string GetPropertyName(Expression<Func<TEntity, ICollection<TItem>>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            return "Collection";
        }

        public EntityChangeOperationExecutionResult Execute(TEntity entity, EntityChangeOrderedExecutionContext<TEntity> context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                var message = errorMessage ?? $"Cannot operate on null collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            var item = collection.FirstOrDefault(x => this.comparer.Equals(x.Id, id));
            if (item == null)
            {
                var message = errorMessage ?? $"Item with ID '{id}' not found in collection '{this.propertyName}'";
                return EntityChangeOperationExecutionResult.Failure(
                    errors: [error ?? new NotFoundError(message)]);
            }

            var result = action(item);
            if (result.IsFailure)
            {
                return EntityChangeOperationExecutionResult.Failure(result.Errors, result.Messages);
            }

            context.RecordChange(this.propertyName, "Collection");
            return EntityChangeOperationExecutionResult.Success(hasChanged: true);
        }
    }
}