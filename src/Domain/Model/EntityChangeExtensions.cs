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
/// Provides extension methods for entities to enable fluent,
/// transactional-style state changes with automatic change tracking and event registration.
/// </summary>
public static class EntityChangeExtensions
{
    /// <summary>
    /// Initiates a fluent change transaction on an entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <returns>A <see cref="EntityChangeBuilder{TEntity}"/> for fluent configuration.</returns>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(c => c.Name, "New Name")
    ///     .Apply();
    /// </code>
    /// </example>
    public static EntityChangeBuilder<TEntity> Change<TEntity>(this TEntity entity)
        where TEntity : IEntity
    {
        return new EntityChangeBuilder<TEntity>(entity);
    }
}

/// <summary>
/// A context object containing details about the changes applied during a specific change transaction.
/// Used primarily within custom domain event factories to access previous values.
/// </summary>
public class EntityChangeContext
{
    private readonly Dictionary<string, (object OldValue, object NewValue)> changes = [];

    /// <summary>
    /// Records a change to a specific property.
    /// </summary>
    internal void RecordChange(string propertyName, object oldValue, object newValue)
    {
        this.changes[propertyName] = (oldValue, newValue);
    }

    /// <summary>
    /// Checks if a specific property was changed during the transaction.
    /// </summary>
    public bool HasChanged(string propertyName) => this.changes.ContainsKey(propertyName);

    /// <summary>
    /// Gets the old value of a modified property.
    /// </summary>
    public T GetOldValue<T>(string propertyName)
    {
        if (this.changes.TryGetValue(propertyName, out var record))
        {
            return (T)record.OldValue;
        }
        return default;
    }

    /// <summary>
    /// Gets the new value of a modified property.
    /// </summary>
    public T GetNewValue<T>(string propertyName)
    {
        if (this.changes.TryGetValue(propertyName, out var record))
        {
            return (T)record.NewValue;
        }
        return default;
    }
}

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
    private readonly List<IOperation> operations = [];
    private readonly List<Func<TEntity, EntityChangeContext, IDomainEvent>> eventFactories = [];
    private readonly List<(Func<TEntity, bool> Predicate, string Message)> validations = [];
    private bool replaceExisting = true;
    private bool registerNoEvents;

    // Global guard that protects the entire change transaction
    private (Func<TEntity, bool> Predicate, string Message)? globalGuard;

    // Initialized to Success. Since Result is a struct, we avoid null checks and rely on IsFailure state.
    private Result chainConstructionFailure = Result.Success();

    /// <summary>
    /// Specifies whether to replace existing domain events of the same type.
    /// </summary>
    public EntityChangeBuilder<TEntity> ReplaceExisting(bool replace = true)
    {
        this.replaceExisting = replace;
        return this;
    }

    /// <summary>
    /// Specifies that no domain events should be registered when changes are applied.
    /// </summary>
    public EntityChangeBuilder<TEntity> RegisterNoEvents(bool noEvents = true)
    {
        this.registerNoEvents = noEvents;
        return this;
    }

    // -------------------------------------------------------------------------
    // 1. Set Operations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Queues a property change with a direct value.
    /// </summary>
    public EntityChangeBuilder<TEntity> Set<TValue>(
        Expression<Func<TEntity, TValue>> propertyExpression,
        TValue newValue,
        IEqualityComparer<TValue> comparer = null)
    {
        this.operations.Add(new SetOperation<TValue>(propertyExpression, _ => newValue, comparer));
        return this;
    }

    /// <summary>
    /// Queues a property change with a computed value.
    /// </summary>
    public EntityChangeBuilder<TEntity> Set<TValue>(
        Expression<Func<TEntity, TValue>> propertyExpression,
        Func<TEntity, TValue> valueFactory,
        IEqualityComparer<TValue> comparer = null)
    {
        this.operations.Add(new SetOperation<TValue>(propertyExpression, valueFactory, comparer));
        return this;
    }

    /// <summary>
    /// Queues a property change using a <see cref="Result{T}"/>.
    /// If the Result is a Failure, the entire transaction will fail when <see cref="Apply"/> is called.
    /// </summary>
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
    public EntityChangeBuilder<TEntity> Set<TValue>(
        Expression<Func<TEntity, TValue>> propertyExpression,
        Func<TEntity, Result<TValue>> valueFactory,
        IEqualityComparer<TValue> comparer = null)
    {
        this.operations.Add(new ResultSetOperation<TValue>(propertyExpression, valueFactory, comparer));
        return this;
    }

    // -------------------------------------------------------------------------
    // 2. Collection Operations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Queues an operation to add an item to a collection property.
    /// </summary>
    public EntityChangeBuilder<TEntity> Add<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TItem item,
        IEqualityComparer<TItem> comparer = null)
    {
        this.operations.Add(new CollectionOperation<TItem>(collectionExpression, item, isAdd: true, comparer));
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
        this.operations.Add(new ResultCollectionOperation<TItem>(collectionExpression, itemFactory, isAdd: true, comparer));
        return this;
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection property.
    /// </summary>
    public EntityChangeBuilder<TEntity> Remove<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        TItem item,
        IEqualityComparer<TItem> comparer = null)
    {
        this.operations.Add(new CollectionOperation<TItem>(collectionExpression, item, isAdd: false, comparer));
        return this;
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection property using a <see cref="Result{T}"/>.
    /// If the Result is a Failure, the entire transaction will fail when <see cref="Apply"/> is called.
    /// </summary>
    public EntityChangeBuilder<TEntity> Remove<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Result<TItem> result,
        IEqualityComparer<TItem> comparer = null)
    {
        if (result.IsFailure)
        {
            if (this.chainConstructionFailure.IsSuccess)
            {
                this.chainConstructionFailure = Result.Failure(result.Messages, result.Errors);
            }
            return this;
        }

        return this.Remove(collectionExpression, result.Value, comparer);
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection property using a function that returns a <see cref="Result{T}"/>.
    /// If the computed Result is a Failure, the transaction stops and returns that failure.
    /// </summary>
    public EntityChangeBuilder<TEntity> Remove<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
        Func<TEntity, Result<TItem>> itemFactory,
        IEqualityComparer<TItem> comparer = null)
    {
        this.operations.Add(new ResultCollectionOperation<TItem>(collectionExpression, itemFactory, isAdd: false, comparer));
        return this;
    }

    /// <summary>
    /// Queues an operation to clear all items from a collection property.
    /// </summary>
    public EntityChangeBuilder<TEntity> Clear<TItem>(
        Expression<Func<TEntity, ICollection<TItem>>> collectionExpression)
    {
        this.operations.Add(new ClearCollectionOperation<TItem>(collectionExpression));
        return this;
    }

    // -------------------------------------------------------------------------
    // 3. Logic & Guards
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets a global condition that guards the entire change transaction.
    /// If the predicate returns false, the entire transaction is skipped silently with a successful result.
    /// This must be called at the beginning of the chain before any Set, Add, Remove, or Ensure operations.
    /// </summary>
    /// <param name="predicate">The condition to evaluate against the entity.</param>
    /// <param name="errorMessage">Optional error message (not used for silent skip, informational only).</param>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .When(_ => age != 0)  // Guard: skip entire chain if age is 0
    ///     .Ensure(p => age >= 0, "Age must be non-negative")
    ///     .Set(p => p.Age, age)
    ///     .Apply();
    /// </code>
    /// </example>
    public EntityChangeBuilder<TEntity> When(Func<TEntity, bool> predicate, string errorMessage = null)
    {
        this.globalGuard = (predicate, errorMessage ?? "When condition not met");
        return this;
    }

    /// <summary>
    /// Adds a pre-condition check. If the predicate returns false, the transaction aborts.
    /// This runs *after* the global When() guard but *before* any Set/Add/Remove operations.
    /// </summary>
    public EntityChangeBuilder<TEntity> Ensure(Func<TEntity, bool> predicate, string errorMessage)
    {
        this.operations.Add(new EnsureOperation(predicate, errorMessage));
        return this;
    }

    /// <summary>
    /// Executes an arbitrary action on the entity.
    /// </summary>
    public EntityChangeBuilder<TEntity> Execute(Action<TEntity> action)
    {
        this.operations.Add(new ExecuteOperation(action));
        return this;
    }

    // -------------------------------------------------------------------------
    // 4. Validation & Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a check rule that runs *after* changes have been applied.
    /// </summary>
    public EntityChangeBuilder<TEntity> Check(Func<TEntity, bool> predicate, string errorMessage)
    {
        this.validations.Add((predicate, errorMessage));
        return this;
    }

    /// <summary>
    /// Registers a custom domain event to be registered if changes occur.
    /// Note: This will throw an InvalidOperationException at runtime if the entity does not implement IAggregateRoot.
    /// </summary>
    public EntityChangeBuilder<TEntity> Register<TEvent>(Func<TEntity, EntityChangeContext, TEvent> eventFactory)
        where TEvent : IDomainEvent
    {
        this.eventFactories.Add((agg, ctx) => eventFactory(agg, ctx));
        return this;
    }

    /// <summary>
    /// Registers a custom domain event to be registered if changes occur.
    /// Note: This will throw an InvalidOperationException at runtime if the entity does not implement IAggregateRoot.
    /// </summary>
    public EntityChangeBuilder<TEntity> Register<TEvent>(Func<TEntity, TEvent> eventFactory)
        where TEvent : IDomainEvent
    {
        this.eventFactories.Add((agg, _) => eventFactory(agg));
        return this;
    }

    // -------------------------------------------------------------------------
    // 5. Apply
    // -------------------------------------------------------------------------

    /// <summary>
    /// Executes the change transaction.
    /// <para>
    /// Steps:
    /// 1. Evaluates the global When() guard. If false, returns success without changes.
    /// 2. Checks for fast-fail errors from Result-based Set operations.
    /// 3. Executes all queued operations (Ensure, Set, Add, Remove, Execute).
    /// 4. Checks post-change validations.
    /// 5. Registers domain events if changes were made (only for IAggregateRoot instances).
    /// </para>
    /// </summary>
    public Result<TEntity> Apply()
    {
        // 1. Check global guard FIRST
        // If When() condition is false, skip entire transaction silently
        if (this.globalGuard.HasValue && !this.globalGuard.Value.Predicate(entity))
        {
            return Result<TEntity>.Success(entity);
        }

        // 2. Check for fast-fail errors captured during chaining
        if (this.chainConstructionFailure.IsFailure)
        {
            return Result<TEntity>.Failure(entity)
                .WithMessages(this.chainConstructionFailure.Messages)
                .WithErrors(this.chainConstructionFailure.Errors);
        }

        var context = new EntityChangeContext();
        var changesMade = false;

        // 3. Execute Operations
        foreach (var op in this.operations)
        {
            var opResult = op.Execute(entity, context);
            if (opResult.IsFailure)
            {
                return Result<TEntity>.Failure(entity)
                    .WithErrors(opResult.Errors);
            }

            if (opResult.HasChanged)
            {
                changesMade = true;
            }
        }

        if (!changesMade)
        {
            return Result<TEntity>.Success(entity);
        }

        // 4. Post-Change Validation
        foreach (var (predicate, message) in this.validations)
        {
            if (!predicate(entity))
            {
                return Result<TEntity>.Failure(entity, message);
            }
        }

        // 5. Register Events (only if entity implements IAggregateRoot)
        if (!this.registerNoEvents)
        {
            if (entity is IAggregateRoot aggregateRoot) // Check if entity implements IAggregateRoot at runtime
            {
                foreach (var factory in this.eventFactories)
                {
                    var evt = factory(entity, context);
                    aggregateRoot.DomainEvents.Register(evt, this.replaceExisting);
                }

                if (this.eventFactories.Count == 0)
                {
                    // Create the event using reflection since TEntity might not constrain to IAggregateRoot
                    var eventType = typeof(EntityUpdatedDomainEvent<>).MakeGenericType(entity.GetType());
                    var domainEvent = (IDomainEvent)Activator.CreateInstance(eventType, entity);
                    aggregateRoot.DomainEvents.Register(domainEvent, this.replaceExisting);
                }
            }
            else if (this.eventFactories.Count > 0)
            {
                // Events were registered, but entity is not an aggregate root
                throw new InvalidOperationException(
                    $"Cannot register domain events on entity of type '{typeof(TEntity).Name}' because it does not implement IAggregateRoot. Only aggregate roots can register domain events.");
            }
        }

        return Result<TEntity>.Success(entity);
    }

    // -------------------------------------------------------------------------
    // Internal Infrastructure
    // -------------------------------------------------------------------------

    /// <summary>
    /// Represents the result of an individual change operation.
    /// </summary>
    private readonly struct OpResult
    {
        public bool IsFailure { get; }

        public bool HasChanged { get; }

        public IEnumerable<IResultError> Errors { get; }

        private OpResult(bool isFailure, bool hasChanged, IEnumerable<IResultError> errors)
        {
            this.IsFailure = isFailure;
            this.HasChanged = hasChanged;
            this.Errors = errors ?? [];
        }

        public static OpResult Success(bool changed) => new(false, changed, null);

        public static OpResult Failure(IEnumerable<IResultError> errors) => new(true, false, errors);
    }

    private interface IOperation
    {
        void AddCondition(Func<TEntity, bool> predicate);

        OpResult Execute(TEntity entity, EntityChangeContext context);
    }

    private abstract class OperationBase : IOperation
    {
        protected List<Func<TEntity, bool>> Conditions { get; } = [];

        public void AddCondition(Func<TEntity, bool> predicate) => this.Conditions.Add(predicate);

        public OpResult Execute(TEntity entity, EntityChangeContext context)
        {
            foreach (var condition in this.Conditions)
            {
                if (!condition(entity))
                {
                    return OpResult.Success(false);
                }
            }
            return this.ApplyChange(entity, context);
        }

        protected abstract OpResult ApplyChange(TEntity entity, EntityChangeContext context);
    }

    private class SetOperation<TValue> : OperationBase
    {
        private readonly PropertyAccessor<TValue> accessor;
        private readonly Func<TEntity, TValue> valueFactory;
        private readonly IEqualityComparer<TValue> comparer;
        private readonly string propertyName;

        public SetOperation(
            Expression<Func<TEntity, TValue>> propertyExpression,
            Func<TEntity, TValue> valueFactory,
            IEqualityComparer<TValue> comparer)
        {
            this.accessor = new PropertyAccessor<TValue>(propertyExpression);
            this.valueFactory = valueFactory;
            this.comparer = comparer ?? EqualityComparer<TValue>.Default;
            this.propertyName = this.accessor.PropertyInfo.Name;
        }

        protected override OpResult ApplyChange(TEntity entity, EntityChangeContext context)
        {
            var currentValue = this.accessor.GetValue(entity);
            var newValue = this.valueFactory(entity);

            if (this.comparer.Equals(currentValue, newValue))
            {
                return OpResult.Success(false);
            }

            this.accessor.SetValue(entity, newValue);
            context.RecordChange(this.propertyName, currentValue, newValue);
            return OpResult.Success(true);
        }
    }

    private class ResultSetOperation<TValue> : OperationBase
    {
        private readonly PropertyAccessor<TValue> accessor;
        private readonly Func<TEntity, Result<TValue>> valueFactory;
        private readonly IEqualityComparer<TValue> comparer;
        private readonly string propertyName;

        public ResultSetOperation(
            Expression<Func<TEntity, TValue>> propertyExpression,
            Func<TEntity, Result<TValue>> valueFactory,
            IEqualityComparer<TValue> comparer)
        {
            this.accessor = new PropertyAccessor<TValue>(propertyExpression);
            this.valueFactory = valueFactory;
            this.comparer = comparer ?? EqualityComparer<TValue>.Default;
            this.propertyName = this.accessor.PropertyInfo.Name;
        }

        protected override OpResult ApplyChange(TEntity entity, EntityChangeContext context)
        {
            var result = this.valueFactory(entity);
            if (result.IsFailure)
            {
                return OpResult.Failure(result.Errors);
            }

            var currentValue = this.accessor.GetValue(entity);
            if (this.comparer.Equals(currentValue, result.Value))
            {
                return OpResult.Success(false);
            }

            this.accessor.SetValue(entity, result.Value);
            context.RecordChange(this.propertyName, currentValue, result.Value);
            return OpResult.Success(true);
        }
    }

    private class CollectionOperation<TItem> : OperationBase
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter;
        private readonly TItem item;
        private readonly bool isAdd;
        private readonly string propertyName;
        private readonly IEqualityComparer<TItem> comparer;

        public CollectionOperation(
            Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
            TItem item,
            bool isAdd,
            IEqualityComparer<TItem> comparer)
        {
            // Handle both properties and fields (backing fields for properties)
            var memberExpression = collectionExpression.Body as MemberExpression
                ?? throw new ArgumentException("Expression must be a member access", nameof(collectionExpression));

            var member = memberExpression.Member;
            if (member is not PropertyInfo && member is not FieldInfo)
            {
                throw new ArgumentException("Expression must be a property or field", nameof(collectionExpression));
            }

            this.propertyName = member.Name;
            this.collectionGetter = collectionExpression.Compile();
            this.item = item;
            this.isAdd = isAdd;
            this.comparer = comparer ?? EqualityComparer<TItem>.Default;
        }

        protected override OpResult ApplyChange(TEntity entity, EntityChangeContext context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                return OpResult.Success(false);
            }

            bool exists;
            if (this.comparer == EqualityComparer<TItem>.Default)
            {
                exists = collection.Contains(this.item);
            }
            else
            {
                exists = collection.Any(x => this.comparer.Equals(x, this.item));
            }

            if (this.isAdd)
            {
                if (exists)
                {
                    return OpResult.Success(false);
                }

                collection.Add(this.item);
                context.RecordChange(this.propertyName, "Collection", "Item Added");
                return OpResult.Success(true);
            }
            else
            {
                if (!exists)
                {
                    return OpResult.Success(false);
                }

                if (this.comparer != EqualityComparer<TItem>.Default)
                {
                    var itemToRemove = collection.First(x => this.comparer.Equals(x, this.item));
                    collection.Remove(itemToRemove);
                }
                else
                {
                    collection.Remove(this.item);
                }
                context.RecordChange(this.propertyName, "Collection", "Item Removed");
                return OpResult.Success(true);
            }
        }
    }

    private class ResultCollectionOperation<TItem> : OperationBase
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter;
        private readonly Func<TEntity, Result<TItem>> itemFactory;
        private readonly bool isAdd;
        private readonly string propertyName;
        private readonly IEqualityComparer<TItem> comparer;

        public ResultCollectionOperation(
            Expression<Func<TEntity, ICollection<TItem>>> collectionExpression,
            Func<TEntity, Result<TItem>> itemFactory,
            bool isAdd,
            IEqualityComparer<TItem> comparer)
        {
            // Handle both properties and fields (backing fields for properties)
            var memberExpression = collectionExpression.Body as MemberExpression
                ?? throw new ArgumentException("Expression must be a member access", nameof(collectionExpression));

            var member = memberExpression.Member;
            if (member is not PropertyInfo && member is not FieldInfo)
            {
                throw new ArgumentException("Expression must be a property or field", nameof(collectionExpression));
            }

            this.propertyName = member.Name;
            this.collectionGetter = collectionExpression.Compile();
            this.itemFactory = itemFactory;
            this.isAdd = isAdd;
            this.comparer = comparer ?? EqualityComparer<TItem>.Default;
        }

        protected override OpResult ApplyChange(TEntity entity, EntityChangeContext context)
        {
            // 1. Get the item from the factory (which returns a Result)
            var result = this.itemFactory(entity);
            if (result.IsFailure)
            {
                return OpResult.Failure(result.Errors);
            }

            var item = result.Value;
            var collection = this.collectionGetter(entity);
            if (collection == null)
            {
                return OpResult.Success(false);
            }

            // 2. Check if item exists in collection
            bool exists;
            if (this.comparer == EqualityComparer<TItem>.Default)
            {
                exists = collection.Contains(item);
            }
            else
            {
                exists = collection.Any(x => this.comparer.Equals(x, item));
            }

            if (this.isAdd)
            {
                if (exists)
                {
                    return OpResult.Success(false);
                }

                collection.Add(item);
                context.RecordChange(this.propertyName, "Collection", "Item Added");
                return OpResult.Success(true);
            }
            else // Remove
            {
                if (!exists)
                {
                    return OpResult.Success(false);
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
                context.RecordChange(this.propertyName, "Collection", "Item Removed");
                return OpResult.Success(true);
            }
        }
    }

    private class ClearCollectionOperation<TItem> : OperationBase
    {
        private readonly Func<TEntity, ICollection<TItem>> collectionGetter;
        private readonly string propertyName;

        public ClearCollectionOperation(Expression<Func<TEntity, ICollection<TItem>>> collectionExpression)
        {
            // Handle both properties and fields (backing fields for properties)
            var memberExpression = collectionExpression.Body as MemberExpression
                ?? throw new ArgumentException("Expression must be a member access", nameof(collectionExpression));

            var member = memberExpression.Member;
            if (member is not PropertyInfo && member is not FieldInfo)
            {
                throw new ArgumentException("Expression must be a property or field", nameof(collectionExpression));
            }

            this.propertyName = member.Name;
            this.collectionGetter = collectionExpression.Compile();
        }

        protected override OpResult ApplyChange(TEntity entity, EntityChangeContext context)
        {
            var collection = this.collectionGetter(entity);
            if (collection == null || collection.Count == 0)
            {
                return OpResult.Success(false);
            }

            collection.Clear();
            context.RecordChange(this.propertyName, "Collection", "Cleared");
            return OpResult.Success(true);
        }
    }

    private class EnsureOperation(Func<TEntity, bool> predicate, string errorMessage) : OperationBase
    {
        protected override OpResult ApplyChange(TEntity entity, EntityChangeContext context)
        {
            if (!predicate(entity))
            {
                return OpResult.Failure([new Error(errorMessage)]);
            }

            return OpResult.Success(false);
        }
    }

    private class ExecuteOperation(Action<TEntity> action) : OperationBase
    {
        protected override OpResult ApplyChange(TEntity entity, EntityChangeContext context)
        {
            action(entity);
            context.RecordChange("Execute", null, "Action Executed");
            return OpResult.Success(true);
        }
    }

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
}