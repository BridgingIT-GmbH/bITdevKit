// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using BridgingIT.DevKit.Common; // Assuming Result definition is here based on provided snippet
using BridgingIT.DevKit.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Provides extension methods for the Aggregate Root pattern to enable fluent,
/// transactional-style state changes with automatic change tracking and event registration.
/// </summary>
public static class AggregateRootExtensions
{
    /// <summary>
    /// Initiates a fluent change transaction on an aggregate root.
    /// </summary>
    /// <typeparam name="TAggregate">The type of the aggregate root.</typeparam>
    /// <param name="aggregate">The aggregate root instance.</param>
    /// <returns>A <see cref="AggregateRootChangeBuilder{TAggregate}"/> for fluent configuration.</returns>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(c => c.Name, "New Name")
    ///     .Apply();
    /// </code>
    /// </example>
    public static AggregateRootChangeBuilder<TAggregate> Change<TAggregate>(this TAggregate aggregate)
        where TAggregate : IAggregateRoot
    {
        return new AggregateRootChangeBuilder<TAggregate>(aggregate);
    }
}

/// <summary>
/// A context object containing details about the changes applied during a specific change transaction.
/// Used primarily within custom domain event factories to access previous values.
/// </summary>
public class AggregateRootChangeContext
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
/// Fluent builder for applying complex changes to aggregate roots.
/// </summary>
/// <typeparam name="TAggregate">The type of the aggregate root.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="AggregateRootChangeBuilder{TAggregate}"/> class.
/// </remarks>
public class AggregateRootChangeBuilder<TAggregate>(TAggregate aggregate)
    where TAggregate : IAggregateRoot
{
    private readonly List<IOperation> operations = [];
    private readonly List<Func<TAggregate, AggregateRootChangeContext, IDomainEvent>> eventFactories = [];
    private readonly List<(Func<TAggregate, bool> Predicate, string Message)> validations = [];
    private bool replaceExisting = true;
    private bool registerNoEvents = false;

    // Initialized to Success. Since Result is a struct, we avoid null checks and rely on IsFailure state.
    private Result chainConstructionFailure = Result.Success();

    /// <summary>
    /// Specifies whether to replace existing domain events of the same type.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> ReplaceExisting(bool replace = true)
    {
        this.replaceExisting = replace;
        return this;
    }

    public AggregateRootChangeBuilder<TAggregate> RegisterNoEvents(bool noEvents = true)
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
    public AggregateRootChangeBuilder<TAggregate> Set<TValue>(
        Expression<Func<TAggregate, TValue>> propertyExpression,
        TValue newValue,
        IEqualityComparer<TValue> comparer = null)
    {
        this.operations.Add(new SetOperation<TValue>(propertyExpression, _ => newValue, comparer));
        return this;
    }

    /// <summary>
    /// Queues a property change with a computed value.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> Set<TValue>(
        Expression<Func<TAggregate, TValue>> propertyExpression,
        Func<TAggregate, TValue> valueFactory,
        IEqualityComparer<TValue> comparer = null)
    {
        this.operations.Add(new SetOperation<TValue>(propertyExpression, valueFactory, comparer));
        return this;
    }

    /// <summary>
    /// Queues a property change using a <see cref="Result{T}"/>.
    /// If the Result is a Failure, the entire transaction will fail when <see cref="Apply"/> is called.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> Set<TValue>(
        Expression<Func<TAggregate, TValue>> propertyExpression,
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
    public AggregateRootChangeBuilder<TAggregate> Set<TValue>(
        Expression<Func<TAggregate, TValue>> propertyExpression,
        Func<TAggregate, Result<TValue>> valueFactory,
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
    public AggregateRootChangeBuilder<TAggregate> Add<TItem>(
        Expression<Func<TAggregate, ICollection<TItem>>> collectionExpression,
        TItem item,
        IEqualityComparer<TItem> comparer = null)
    {
        this.operations.Add(new CollectionOperation<TItem>(collectionExpression, item, isAdd: true, comparer));
        return this;
    }

    /// <summary>
    /// Queues an operation to remove an item from a collection property.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> Remove<TItem>(
        Expression<Func<TAggregate, ICollection<TItem>>> collectionExpression,
        TItem item,
        IEqualityComparer<TItem> comparer = null)
    {
        this.operations.Add(new CollectionOperation<TItem>(collectionExpression, item, isAdd: false, comparer));
        return this;
    }

    /// <summary>
    /// Queues an operation to clear all items from a collection property.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> Clear<TItem>(
        Expression<Func<TAggregate, ICollection<TItem>>> collectionExpression)
    {
        this.operations.Add(new ClearCollectionOperation<TItem>(collectionExpression));
        return this;
    }

    // -------------------------------------------------------------------------
    // 3. Logic & Guards
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a pre-condition check. If the predicate returns false, the transaction aborts.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> Ensure(Func<TAggregate, bool> predicate, string errorMessage)
    {
        this.operations.Add(new EnsureOperation(predicate, errorMessage));
        return this;
    }

    /// <summary>
    /// Executes an arbitrary action on the aggregate.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> Execute(Action<TAggregate> action)
    {
        this.operations.Add(new ExecuteOperation(action));
        return this;
    }

    /// <summary>
    /// Applies a condition to the *immediately preceding* operation.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> When(Func<TAggregate, bool> predicate)
    {
        if (this.operations.Count == 0)
        {
            throw new InvalidOperationException("When() must be called after an operation.");
        }

        var lastOp = this.operations[this.operations.Count - 1];
        lastOp.AddCondition(predicate);

        return this;
    }

    // -------------------------------------------------------------------------
    // 4. Validation & Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a validation rule that runs *after* changes have been applied.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> Validate(Func<TAggregate, bool> predicate, string errorMessage)
    {
        this.validations.Add((predicate, errorMessage));

        return this;
    }

    /// <summary>
    /// Registers a custom domain event to be registered if changes occur.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> Register<TEvent>(Func<TAggregate, AggregateRootChangeContext, TEvent> eventFactory)
        where TEvent : IDomainEvent
    {
        this.eventFactories.Add((agg, ctx) => eventFactory(agg, ctx));

        return this;
    }

    /// <summary>
    /// Registers a custom domain event to be registered if changes occur.
    /// </summary>
    public AggregateRootChangeBuilder<TAggregate> Register<TEvent>(Func<TAggregate, TEvent> eventFactory)
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
    /// </summary>
    public Result<TAggregate> Apply()
    {
        // 1. Check for fast-fail errors captured during chaining
        if (this.chainConstructionFailure.IsFailure)
        {
            return Result<TAggregate>.Failure(aggregate)
                .WithMessages(this.chainConstructionFailure.Messages)
                .WithErrors(this.chainConstructionFailure.Errors);
        }

        var context = new AggregateRootChangeContext();
        var changesMade = false;

        // 2. Execute Operations
        foreach (var op in this.operations)
        {
            var opResult = op.Execute(aggregate, context);

            if (opResult.IsFailure)
            {
                return Result<TAggregate>.Failure(aggregate)
                    .WithErrors(opResult.Errors);
            }

            if (opResult.HasChanged)
            {
                changesMade = true;
            }
        }

        if (!changesMade)
        {
            return Result<TAggregate>.Success(aggregate);
        }

        // 3. Post-Change Validation
        foreach (var (predicate, message) in this.validations)
        {
            if (!predicate(aggregate))
            {
                return Result<TAggregate>.Failure(aggregate, message);
            }
        }

        // 4. Register Events
        if (!this.registerNoEvents) // Skip event registration if flag is set
        {
            foreach (var factory in this.eventFactories)
            {
                var evt = factory(aggregate, context);
                aggregate.DomainEvents.Register(evt, this.replaceExisting);
            }

            if (this.eventFactories.Count == 0) // Default event if none registered
            {
                aggregate.DomainEvents.Register(
                    new EntityUpdatedDomainEvent<TAggregate>(aggregate),
                    this.replaceExisting);
            }
        }

        return Result<TAggregate>.Success(aggregate);
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
        void AddCondition(Func<TAggregate, bool> predicate);
        OpResult Execute(TAggregate aggregate, AggregateRootChangeContext context);
    }

    private abstract class OperationBase : IOperation
    {
        protected List<Func<TAggregate, bool>> Conditions { get; } = [];

        public void AddCondition(Func<TAggregate, bool> predicate) => this.Conditions.Add(predicate);

        public OpResult Execute(TAggregate aggregate, AggregateRootChangeContext context)
        {
            foreach (var condition in this.Conditions)
            {
                if (!condition(aggregate))
                {
                    return OpResult.Success(false);
                }
            }
            return this.ApplyChange(aggregate, context);
        }

        protected abstract OpResult ApplyChange(TAggregate aggregate, AggregateRootChangeContext context);
    }

    private class SetOperation<TValue> : OperationBase
    {
        private readonly PropertyAccessor<TValue> accessor;
        private readonly Func<TAggregate, TValue> valueFactory;
        private readonly IEqualityComparer<TValue> comparer;
        private readonly string propertyName;

        public SetOperation(
            Expression<Func<TAggregate, TValue>> propertyExpression,
            Func<TAggregate, TValue> valueFactory,
            IEqualityComparer<TValue> comparer)
        {
            this.accessor = new PropertyAccessor<TValue>(propertyExpression);
            this.valueFactory = valueFactory;
            this.comparer = comparer ?? EqualityComparer<TValue>.Default;
            this.propertyName = this.accessor.PropertyInfo.Name;
        }

        protected override OpResult ApplyChange(TAggregate aggregate, AggregateRootChangeContext context)
        {
            var currentValue = this.accessor.GetValue(aggregate);
            var newValue = this.valueFactory(aggregate);

            if (this.comparer.Equals(currentValue, newValue))
            {
                return OpResult.Success(false);
            }

            this.accessor.SetValue(aggregate, newValue);
            context.RecordChange(this.propertyName, currentValue, newValue);
            return OpResult.Success(true);
        }
    }

    private class ResultSetOperation<TValue> : OperationBase
    {
        private readonly PropertyAccessor<TValue> accessor;
        private readonly Func<TAggregate, Result<TValue>> valueFactory;
        private readonly IEqualityComparer<TValue> comparer;
        private readonly string propertyName;

        public ResultSetOperation(
            Expression<Func<TAggregate, TValue>> propertyExpression,
            Func<TAggregate, Result<TValue>> valueFactory,
            IEqualityComparer<TValue> comparer)
        {
            this.accessor = new PropertyAccessor<TValue>(propertyExpression);
            this.valueFactory = valueFactory;
            this.comparer = comparer ?? EqualityComparer<TValue>.Default;
            this.propertyName = this.accessor.PropertyInfo.Name;
        }

        protected override OpResult ApplyChange(TAggregate aggregate, AggregateRootChangeContext context)
        {
            var result = this.valueFactory(aggregate);
            if (result.IsFailure)
            {
                return OpResult.Failure(result.Errors);
            }

            var currentValue = this.accessor.GetValue(aggregate);
            if (this.comparer.Equals(currentValue, result.Value))
            {
                return OpResult.Success(false);
            }

            this.accessor.SetValue(aggregate, result.Value);
            context.RecordChange(this.propertyName, currentValue, result.Value);
            return OpResult.Success(true);
        }
    }

    private class CollectionOperation<TItem> : OperationBase
    {
        private readonly Func<TAggregate, ICollection<TItem>> collectionGetter;
        private readonly TItem item;
        private readonly bool isAdd;
        private readonly string propertyName;
        private readonly IEqualityComparer<TItem> comparer;

        public CollectionOperation(
            Expression<Func<TAggregate, ICollection<TItem>>> collectionExpression,
            TItem item,
            bool isAdd,
            IEqualityComparer<TItem> comparer)
        {
            var member = (collectionExpression.Body as MemberExpression)?.Member as PropertyInfo ?? throw new ArgumentException("Expression must be a property", nameof(collectionExpression));
            this.propertyName = member.Name;
            this.collectionGetter = collectionExpression.Compile();
            this.item = item;
            this.isAdd = isAdd;
            this.comparer = comparer ?? EqualityComparer<TItem>.Default;
        }

        protected override OpResult ApplyChange(TAggregate aggregate, AggregateRootChangeContext context)
        {
            var collection = this.collectionGetter(aggregate);
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

    private class ClearCollectionOperation<TItem> : OperationBase
    {
        private readonly Func<TAggregate, ICollection<TItem>> collectionGetter;
        private readonly string propertyName;

        public ClearCollectionOperation(Expression<Func<TAggregate, ICollection<TItem>>> collectionExpression)
        {
            var member = (collectionExpression.Body as MemberExpression)?.Member as PropertyInfo ?? throw new ArgumentException("Expression must be a property");
            this.propertyName = member.Name;
            this.collectionGetter = collectionExpression.Compile();
        }

        protected override OpResult ApplyChange(TAggregate aggregate, AggregateRootChangeContext context)
        {
            var collection = this.collectionGetter(aggregate);
            if (collection == null || collection.Count == 0)
            {
                return OpResult.Success(false);
            }

            collection.Clear();
            context.RecordChange(this.propertyName, "Collection", "Cleared");
            return OpResult.Success(true);
        }
    }

    private class EnsureOperation(Func<TAggregate, bool> predicate, string errorMessage) : OperationBase
    {
        protected override OpResult ApplyChange(TAggregate aggregate, AggregateRootChangeContext context)
        {
            if (!predicate(aggregate))
            {
                // Assuming Error is a basic implementation of IResultError.
                // Replace "new Error(errorMessage)" with your specific error implementation if needed.
                return OpResult.Failure([new Error(errorMessage)]);
            }

            return OpResult.Success(false);
        }
    }

    private class ExecuteOperation(Action<TAggregate> action) : OperationBase
    {
        protected override OpResult ApplyChange(TAggregate aggregate, AggregateRootChangeContext context)
        {
            action(aggregate);
            context.RecordChange("Execute", null, "Action Executed");
            return OpResult.Success(true);
        }
    }

    private class PropertyAccessor<TValue>
    {
        public PropertyInfo PropertyInfo { get; }
        private readonly Func<TAggregate, TValue> getter;
        private readonly Action<TAggregate, TValue> setter;
        private static readonly ConcurrentDictionary<string, (Func<TAggregate, TValue>, Action<TAggregate, TValue>)> Cache = [];

        public PropertyAccessor(Expression<Func<TAggregate, TValue>> propertyExpression)
        {
            var memberExpression = propertyExpression.Body as MemberExpression ?? throw new ArgumentException("Expression must be a property access");
            this.PropertyInfo = memberExpression.Member as PropertyInfo ?? throw new ArgumentException("Expression must reference a property");
            var key = $"{typeof(TAggregate).FullName}.{this.PropertyInfo.Name}";

            var accessors = Cache.GetOrAdd(key, _ =>
            {
                var instanceParam = Expression.Parameter(typeof(TAggregate), "instance");
                var propertyAccess = Expression.Property(instanceParam, this.PropertyInfo);
                var compiledGetter = Expression.Lambda<Func<TAggregate, TValue>>(propertyAccess, instanceParam).Compile();

                var valueParam = Expression.Parameter(typeof(TValue), "value");
                var assignExpression = Expression.Assign(Expression.Property(instanceParam, this.PropertyInfo), valueParam);
                var compiledSetter = Expression.Lambda<Action<TAggregate, TValue>>(assignExpression, instanceParam, valueParam).Compile();

                return (compiledGetter, compiledSetter);
            });

            this.getter = accessors.Item1;
            this.setter = accessors.Item2;
        }

        public TValue GetValue(TAggregate instance) => this.getter(instance);
        public void SetValue(TAggregate instance, TValue value) => this.setter(instance, value);
    }
}