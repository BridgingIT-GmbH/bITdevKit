// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

/// <summary>
/// EF Core–specific implementation of <see cref="IEntityUpdateSet{TEntity}"/>.
/// Collects property assignments in a type-safe way and translates them
/// into EF Core <c>ExecuteUpdateAsync</c> calls.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// This class is internal to the EF Core active entity provider/repository and should not be used directly
/// by consumers. Instead, use the <c>UpdateAsync</c> methods on the provider,
/// which accept an <see cref="Action{IEntityUpdateSet{TEntity}}"/> to configure updates.
/// </remarks>
/// <example>
/// Example usage through the active entity/repository provider:
/// <code>
/// var result = await provider|repository.UpdateAsync(
///     new Specification&lt;User&gt;(u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-1)),
///     set => set
///         .Set(u => u.IsActive, false)                 // constant
///         .Set(u => u.LoginCount, u => u.LoginCount+1) // computed
///         .Set(u => u.LastLogin, u => DateTime.UtcNow) // dynamic
/// );
///
/// if (result.IsSuccess)
/// {
///     Console.WriteLine($"Updated {result.Value} users");
/// }
/// </code>
/// </example>
public class EntityFrameworkEntityUpdateSet<TEntity> : IEntityUpdateSet<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets the list of property assignments collected by this update set.
    /// </summary>
    /// <remarks>
    /// In EF Core 10+, ExecuteUpdateAsync accepts an Action instead of Expression,
    /// making dynamic updates much simpler. We store the assignments and apply them
    /// in the ExecuteUpdateAsync lambda.
    /// </remarks>
    private readonly List<Assignment> assignments = [];

    /// <summary>
    /// Represents a single property assignment with its selector and value.
    /// </summary>
    public class Assignment
    {
        public LambdaExpression PropertySelector { get; set; }

        public object Value { get; set; }

        public LambdaExpression ValueFactory { get; set; }

        public bool IsComputed => this.ValueFactory != null;
    }

    public IReadOnlyList<Assignment> Assignments => this.assignments;

    /// <summary>
    /// Adds a property assignment to the update set with a constant value.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being updated.</typeparam>
    /// <param name="property">The property selector expression.</param>
    /// <param name="value">The constant value to assign to the property.</param>
    /// <returns>The current <see cref="IEntityUpdateSet{TEntity}"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// set => set
    ///     .Set(u => u.IsActive, false)
    ///     .Set(u => u.Status, "Inactive")
    /// </code>
    /// </example>
    public IEntityUpdateSet<TEntity> Set<TProperty>(Expression<Func<TEntity, TProperty>> property, TProperty value)
    {
        this.assignments.Add(new Assignment
        {
            PropertySelector = property,
            Value = value,
            ValueFactory = null
        });

        return this;
    }

    /// <summary>
    /// Adds a property assignment to the update set with a computed value
    /// based on the entity itself.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being updated.</typeparam>
    /// <param name="property">The property selector expression.</param>
    /// <param name="valueFactory">An expression that computes the new value from the entity.</param>
    /// <returns>The current <see cref="IEntityUpdateSet{TEntity}"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// set => set
    ///     .Set(u => u.LoginCount, u => u.LoginCount + 1)
    ///     .Set(u => u.LastLogin, u => DateTime.UtcNow)
    /// </code>
    /// </example>
    public IEntityUpdateSet<TEntity> Set<TProperty>(Expression<Func<TEntity, TProperty>> property, Expression<Func<TEntity, TProperty>> valueFactory)
    {
        this.assignments.Add(new Assignment
        {
            PropertySelector = property,
            Value = null,
            ValueFactory = valueFactory
        });

        return this;
    }

    /// <summary>
    /// Applies all collected assignments to the provided setters action.
    /// This method is called internally by the EF Core provider.
    /// </summary>
    internal void ApplyTo(dynamic setters)
    {
        foreach (var assignment in this.assignments)
        {
            if (assignment.IsComputed)
            {
                // Call SetProperty with both expressions (for computed values)
                InvokeSetProperty(setters, assignment.PropertySelector, assignment.ValueFactory);
            }
            else
            {
                // Call SetProperty with expression and constant value
                InvokeSetProperty(setters, assignment.PropertySelector, assignment.Value);
            }
        }
    }

    private static void InvokeSetProperty(dynamic setters, LambdaExpression propertySelector, object value)
    {
        // Use dynamic to call SetProperty without knowing the exact type
        // EF Core 10 will handle the strongly-typed method resolution
        if (value is LambdaExpression valueExpr)
        {
            setters.SetProperty(propertySelector, valueExpr);
        }
        else
        {
            setters.SetProperty(propertySelector, value);
        }
    }
}