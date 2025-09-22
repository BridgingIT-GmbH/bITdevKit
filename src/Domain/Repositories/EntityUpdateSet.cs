// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// In-memory implementation of <see cref="IEntityUpdateSet{TEntity}"/>.
/// Collects property assignments and applies them directly to entities
/// in the in-memory context.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// This class is used internally by the <see cref="ActiveEntityInMemoryProvider{TEntity, TId}"/>
/// to simulate bulk update operations in memory.
/// </remarks>
/// <example>
/// Example usage through the provider:
/// <code>
/// var result = await provider.UpdateAsync(
///     new Specification&lt;User&gt;(u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-1)),
///     set => set
///         .Set(u => u.IsActive, _ => false)            // constant
///         .Set(u => u.LoginCount, u => u.LoginCount+1) // computed
/// );
/// </code>
/// </example>
public class EntityUpdateSet<TEntity> : IEntityUpdateSet<TEntity>
    where TEntity : class, IEntity
{
    private readonly List<Action<TEntity>> assignments = [];

    /// <summary>
    /// Adds a property assignment with a constant value.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being updated.</typeparam>
    /// <param name="property">The property selector expression.</param>
    /// <param name="value">The constant value to assign to the property.</param>
    /// <returns>The current <see cref="IEntityUpdateSet{TEntity}"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// set => set.Set(u => u.IsActive, _ => false);
    /// </code>
    /// </example>
    public IEntityUpdateSet<TEntity> Set<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        TProperty value)
    {
        if (property.Body is not MemberExpression memberExpr || memberExpr.Member is not PropertyInfo propInfo)
        {
            throw new ArgumentException("Property expression must point to a valid property.", nameof(property));
        }

        this.assignments.Add(entity => propInfo.SetValue(entity, value));
        return this;
    }

    /// <summary>
    /// Adds a property assignment with a computed value based on the entity itself.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being updated.</typeparam>
    /// <param name="property">The property selector expression.</param>
    /// <param name="valueFactory">An expression that computes the new value from the entity.</param>
    /// <returns>The current <see cref="IEntityUpdateSet{TEntity}"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// set => set.Set(u => u.LoginCount, u => u.LoginCount + 1);
    /// </code>
    /// </example>
    public IEntityUpdateSet<TEntity> Set<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        Expression<Func<TEntity, TProperty>> valueFactory)
    {
        if (property.Body is not MemberExpression memberExpr || memberExpr.Member is not PropertyInfo propInfo)
        {
            throw new ArgumentException("Property expression must point to a valid property.", nameof(property));
        }

        var compiledValueFactory = valueFactory.Compile();
        this.assignments.Add(entity =>
        {
            var newValue = compiledValueFactory(entity);
            propInfo.SetValue(entity, newValue);
        });

        return this;
    }

    /// <summary>
    /// Applies all collected assignments to the given entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    public void Apply(TEntity entity)
    {
        foreach (var assignment in this.assignments)
        {
            assignment(entity);
        }
    }
}