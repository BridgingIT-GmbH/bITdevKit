// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Extension methods for building fluent ThenInclude chains on IncludeOption.
/// </summary>
public static class IncludeOptionExtensions
{
    /// <summary>
    ///     Specifies additional related data to be further included based on a related type
    ///     that was just included (for reference navigation properties).
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TPreviousProperty">The type of the previously included navigation property.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property to be included.</typeparam>
    /// <param name="source">The source includable option.</param>
    /// <param name="navigationPropertyPath">An expression representing the navigation property to be included.</param>
    /// <returns>An IIncludableOption that can be used to further chain ThenInclude calls.</returns>
    /// <example>
    /// var options = new FindOptions<Customer>()
    ///     .AddInclude(new IncludeOption<Customer, Address>(c => c.BillingAddress)
    ///         .ThenInclude(a => a.City)
    ///         .ThenInclude(c => c.Country));
    /// </example>
    public static IIncludableOption<TEntity, TProperty> ThenInclude<TEntity, TPreviousProperty, TProperty>(
        this IIncludableOption<TEntity, TPreviousProperty> source,
        Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(source, nameof(source));
        EnsureArg.IsNotNull(navigationPropertyPath, nameof(navigationPropertyPath));

        var includeOption = GetIncludeOption<TEntity>(source);

        includeOption.ThenIncludes.Add(new ThenIncludeDescriptor
        {
            Expression = navigationPropertyPath,
            IsCollection = false
        });

        return new IncludableOptionWrapper<TEntity, TProperty>(includeOption);
    }

    /// <summary>
    ///     Specifies additional related data to be further included based on a related type
    ///     that was just included (for collection navigation properties).
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <typeparam name="TPreviousProperty">The element type of the previously included collection navigation property.</typeparam>
    /// <typeparam name="TProperty">The type of the navigation property to be included.</typeparam>
    /// <param name="source">The source includable option.</param>
    /// <param name="navigationPropertyPath">An expression representing the navigation property to be included.</param>
    /// <returns>An IIncludableOption that can be used to further chain ThenInclude calls.</returns>
    /// <example>
    /// var options = new FindOptions<Customer>()
    ///     .AddInclude(new IncludeOption<Customer, ICollection<Order>>(c => c.Orders)
    ///         .ThenInclude(o => o.OrderItems)
    ///         .ThenInclude(i => i.Product));
    /// </example>
    public static IIncludableOption<TEntity, TProperty> ThenInclude<TEntity, TPreviousProperty, TProperty>(
        this IIncludableOption<TEntity, IEnumerable<TPreviousProperty>> source,
        Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(source, nameof(source));
        EnsureArg.IsNotNull(navigationPropertyPath, nameof(navigationPropertyPath));

        var includeOption = GetIncludeOption<TEntity>(source);

        includeOption.ThenIncludes.Add(new ThenIncludeDescriptor
        {
            Expression = navigationPropertyPath,
            IsCollection = true
        });

        return new IncludableOptionWrapper<TEntity, TProperty>(includeOption);
    }

    private static IncludeOptionBase<TEntity> GetIncludeOption<TEntity>(object source)
        where TEntity : class, IEntity
    {
        return source switch
        {
            IncludeOptionBase<TEntity> option => option,
            IncludableOptionWrapper<TEntity> wrapper => wrapper.IncludeOption,
            _ => throw new InvalidOperationException("Invalid source type for ThenInclude")
        };
    }
}

/// <summary>
///     Base wrapper class for type erasure in GetIncludeOption.
/// </summary>
/// <typeparam name="TEntity">The root entity type.</typeparam>
internal abstract class IncludableOptionWrapper<TEntity>
    where TEntity : class, IEntity
{
    public abstract IncludeOptionBase<TEntity> IncludeOption { get; }
}

/// <summary>
///     Internal wrapper class to maintain type information while chaining ThenInclude calls.
/// </summary>
/// <typeparam name="TEntity">The root entity type.</typeparam>
/// <typeparam name="TProperty">The type of the current navigation property.</typeparam>
internal class IncludableOptionWrapper<TEntity, TProperty> : IncludableOptionWrapper<TEntity>, IIncludableOption<TEntity, TProperty>
    where TEntity : class, IEntity
{
    private readonly IncludeOptionBase<TEntity> includeOption;

    public IncludableOptionWrapper(IncludeOptionBase<TEntity> includeOption)
    {
        this.includeOption = includeOption;
    }

    public override IncludeOptionBase<TEntity> IncludeOption => this.includeOption;
}
