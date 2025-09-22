// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// EF Core–specific implementation of <see cref="IEntityUpdateSet{TEntity}"/>.
/// Collects property assignments in a type-safe way and translates them
/// into EF Core <c>ExecuteUpdateAsync</c> expressions.
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
    /// Each assignment is stored as a function that transforms an expression
    /// representing the current <see cref="SetPropertyCalls{TEntity}"/>.
    /// </summary>
    /// <remarks>
    /// These functions are later combined into a single expression tree
    /// inside the provider before being passed to EF Core's
    /// <c>ExecuteUpdateAsync</c>.
    /// </remarks>
    public List<Func<Expression, Expression>> Assignments { get; } = [];

    /// <summary>
    /// Adds a property assignment to the update set with a constant value.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being updated.</typeparam>
    /// <param name="property">The property selector delegate.</param>
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
        this.Assignments.Add(setters =>
            Expression.Call(
                setters,
                typeof(SetPropertyCalls<TEntity>)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m => m.Name == nameof(SetPropertyCalls<TEntity>.SetProperty)
                             && m.GetParameters().Length == 2
                             && m.GetParameters()[1].ParameterType.IsGenericParameter)
                    .MakeGenericMethod(typeof(TProperty)),
                property,                        // property selector
                Expression.Constant(value, typeof(TProperty)) // raw constant
            )
        );

        return this;
    }

    /// <summary>
    /// Adds a property assignment to the update set with a computed value
    /// based on the entity itself.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being updated.</typeparam>
    /// <param name="property">The property selector delegate.</param>
    /// <param name="valueFactory">A delegate that computes the new value from the entity.</param>
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
        this.Assignments.Add(setters =>
            Expression.Call(
                setters,
                typeof(SetPropertyCalls<TEntity>)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m => m.Name == nameof(SetPropertyCalls<TEntity>.SetProperty)
                             && m.GetParameters().Length == 2
                             && m.GetParameters()[1].ParameterType == m.GetParameters()[0].ParameterType)
                    .MakeGenericMethod(typeof(TProperty)),
                property,      // Expression<Func<TEntity,TProperty>>
                valueFactory   // Expression<Func<TEntity,TProperty>>
            )
        );

        return this;
    }
}