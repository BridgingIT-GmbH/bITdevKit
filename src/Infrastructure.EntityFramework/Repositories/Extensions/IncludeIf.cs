// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    /// <summary>
    ///    Includes the specified navigation properties if the options specify includes.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IQueryable<TEntity> IncludeIf<TEntity>(this IQueryable<TEntity> source, IFindOptions<TEntity> options)
        where TEntity : class, IEntity
    {
        if (options is null || options?.HasIncludes() == false)
        {
            return source;
        }

        foreach (var include in (options.Includes.EmptyToNull() ?? []).Insert(options.Include))
        {
            if (include.Expression is not null)
            {
                source = ApplyIncludeWithThenIncludes(source, include);
            }
            else if (!include.Path.IsNullOrEmpty())
            {
                source = source.Include(include.Path);
            }
        }

        return source;
    }

    /// <summary>
    ///   Includes the specified navigation properties if the options specify includes.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TDatabaseEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="options"></param>
    /// <param name="mapper"></param>
    /// <returns></returns>
    public static IQueryable<TDatabaseEntity> IncludeIf<TEntity, TDatabaseEntity>(
        this IQueryable<TDatabaseEntity> source,
        IFindOptions<TEntity> options,
        IEntityMapper mapper)
        where TEntity : class, IEntity
        where TDatabaseEntity : class
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (options is null || options?.HasIncludes() == false)
        {
            return source;
        }

        foreach (var include in (options.Includes.EmptyToNull() ?? []).Insert(options.Include))
        {
            if (include.Expression is not null)
            {
                if (include.ThenIncludes.Count == 0)
                {
                    // Simple include without ThenInclude - use expression-based Include
                    var mappedExpression = mapper.MapExpression<Expression<Func<TDatabaseEntity, object>>>(include.Expression);
                    source = source.Include(mappedExpression);
                }
                else
                {
                    // Build dotted path for ThenIncludes using string-based approach
                    var basePath = include.Path;
                    var fullPath = basePath;

                    foreach (var thenInclude in include.ThenIncludes)
                    {
                        var propertyName = GetPropertyName(thenInclude.Expression);
                        fullPath = $"{fullPath}.{propertyName}";
                    }

                    // Use EF Core's string-based Include for the complete path
                    source = source.Include(fullPath);
                }
            }
            else if (!include.Path.IsNullOrEmpty())
            {
                source = source.Include(include.Path);
            }
        }

        return source;
    }

    private static IQueryable<TEntity> ApplyIncludeWithThenIncludes<TEntity>(
        IQueryable<TEntity> source,
        IncludeOptionBase<TEntity> includeOption)
        where TEntity : class, IEntity
    {
        // Build the full include path with ThenIncludes
        var basePath = includeOption.Path;

        if (includeOption.ThenIncludes.Count == 0)
        {
            // Simple include without ThenInclude - use expression-based Include
            return source.Include(includeOption.Expression);
        }

        // Build dotted path for ThenIncludes (e.g., "Steps.Description" or "BillingAddress.City.Country")
        var fullPath = basePath;
        foreach (var thenInclude in includeOption.ThenIncludes)
        {
            var propertyName = GetPropertyName(thenInclude.Expression);
            fullPath = $"{fullPath}.{propertyName}";
        }

        // Use EF Core's string-based Include for the complete path
        return source.Include(fullPath);
    }

    private static string GetPropertyName(LambdaExpression expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        if (expression.Body is UnaryExpression { Operand: MemberExpression unaryMember })
        {
            return unaryMember.Member.Name;
        }

        throw new ArgumentException($"Cannot extract property name from expression: {expression}");
    }
}