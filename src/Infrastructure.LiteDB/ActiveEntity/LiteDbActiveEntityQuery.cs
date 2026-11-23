// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Linq.Expressions;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Applies ActiveEntity selection, projection inputs, paging, and scalar-count semantics over LiteDB.</summary>
/// <typeparam name="TEntity">The stored entity type.</typeparam>
/// <param name="settings">The provider's frozen collection and materialization settings.</param>
/// <remarks>Invoke query methods on the owning database thread. Returned selections are fully materialized and contain no live cursors.</remarks>
public sealed class LiteDbActiveEntityQuery<TEntity>(LiteDbActiveEntityProviderOptions<TEntity> settings)
    where TEntity : class, IEntity
{
    /// <summary>Contains the detached selected rows and paging metadata.</summary>
    /// <param name="Items">Rows selected after ordering and paging.</param>
    /// <param name="Total">Matching row count before paging.</param>
    /// <param name="Page">One-based page number.</param>
    /// <param name="PageSize">Effective page size, or selected count for an unpaged result.</param>
    public sealed record Selection(IReadOnlyList<TEntity> Items, long Total, int Page, int PageSize);

    /// <summary>Selects detached entities, preserving compound ordering and distinct selection before paging.</summary>
    /// <param name="database">The database owned by the current native executor.</param>
    /// <param name="specifications">Filters combined with AND. Null filters are ignored.</param>
    /// <param name="options">Optional ordering, includes, distinct selection, skip, and take settings.</param>
    /// <param name="paged">Whether to apply default paging of ten rows.</param>
    /// <param name="cancellationToken">Checked after native enumeration, before returning the selection.</param>
    /// <returns>A fully materialized selection with an unpaged total.</returns>
    /// <exception cref="LiteDbProviderException">The selection is invalid, unsupported, or exceeds the materialization limit.</exception>
    public Selection Select(LiteDatabase database, IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options, bool paged, CancellationToken cancellationToken)
    {
        Validate(options);
        var specs = specifications?.Where(s => s is not null).ToArray() ?? [];
        var orders = GetOrders(options);
        var skip = options?.Skip ?? 0;
        var take = options?.Take ?? (paged ? 10 : int.MaxValue);
        var query = this.Build(database, specs, options);
        long total;
        List<TEntity> items;
        if (orders.Count <= 1 && options?.Distinct is null)
        {
            total = this.Build(database, specs, options).LongCount();
            if (orders.Count == 1)
            {
                var order = orders[0];
                query = order.Direction == OrderDirection.Descending ? query.OrderByDescending(order.Expression) : query.OrderBy(order.Expression);
            }

            items = query.Skip(skip).Limit(Math.Min(take, settings.MaxMaterializedRows + 1)).ToList();
            this.CheckLimit(items.Count);
        }
        else
        {
            // A bounded fallback preserves compound ordering and distinct semantics before paging.
            var candidates = query.Limit(settings.MaxMaterializedRows + 1).ToList();
            this.CheckLimit(candidates.Count);
            IEnumerable<TEntity> selection = candidates;
            if (options?.Distinct is not null)
            {
                selection = options.Distinct.Expression is null
                    ? selection.Distinct()
                    : selection.DistinctBy(options.Distinct.Expression.Compile());
            }

            IOrderedEnumerable<TEntity> ordered = null;
            foreach (var order in orders)
            {
                var selector = order.Expression.Compile();
                object Key(TEntity entity) => selector(entity) switch
                {
                    EntityId<Guid> id => id.Value,
                    EntityId<int> id => id.Value,
                    EntityId<long> id => id.Value,
                    EntityId<string> id => id.Value,
                    var value => value
                };

                ordered = ordered is null
                    ? order.Direction == OrderDirection.Descending ? selection.OrderByDescending(Key) : selection.OrderBy(Key)
                    : order.Direction == OrderDirection.Descending ? ordered.ThenByDescending(Key) : ordered.ThenBy(Key);
            }

            selection = ordered ?? selection;
            total = selection.LongCount();
            items = selection.Skip(skip).Take(take).ToList();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new Selection(items, total, paged ? skip / take + 1 : 1, paged ? take : items.Count);
    }

    /// <summary>Counts the selected rows without materializing entities when no distinct fallback is needed.</summary>
    /// <param name="database">The database owned by the current native executor.</param>
    /// <param name="specifications">Filters combined with AND.</param>
    /// <param name="options">Selection settings. Skip and take affect this scalar count.</param>
    /// <param name="cancellationToken">Observed during a bounded distinct selection.</param>
    /// <returns>The selected count, including zero for no matches.</returns>
    public long Count(LiteDatabase database, IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken)
    {
        Validate(options);
        if (options?.Distinct is not null)
        {
            return this.Select(database, specifications, options, false, cancellationToken).Items.Count;
        }

        // Ordering does not affect a scalar count; do not materialize matching entities.
        var count = this.Build(database, specifications, options).LongCount();
        count = Math.Max(0, count - (options?.Skip ?? 0));
        return Math.Min(count, options?.Take ?? long.MaxValue);
    }

    /// <summary>Builds the native filtered and include-aware query without applying ordering or paging.</summary>
    /// <param name="database">The database owned by the current native executor.</param>
    /// <param name="specifications">Filters combined with AND.</param>
    /// <param name="options">Mapped reference or embedded-member include settings.</param>
    /// <returns>A native query to enumerate and dispose on the same thread, before transaction completion.</returns>
    public ILiteQueryable<TEntity> Build(LiteDatabase database, IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options)
    {
        Validate(options);
        var query = database.GetCollection<TEntity>(settings.CollectionName).Query();
        foreach (var specification in specifications ?? [])
        {
            if (specification is not null)
            {
                query = query.Where(specification.ToExpression());
            }
        }

        foreach (var include in ((options?.Includes ?? []).Prepend(options?.Include)).Where(i => i is not null).Distinct())
        {
            var parts = MemberPath(include.Expression, include.Path).ToList();
            foreach (var next in include.ThenIncludes) { parts.AddRange(MemberPath(next.Expression, null)); }

            query = IncludePath(query, database.Mapper, parts);
        }

        if (options?.Hierarchy is { } hierarchy)
        {
            var parts = MemberPath(hierarchy.Expression, hierarchy.Path).ToList();
            var navigation = parts[^1];
            for (var depth = 0; depth < hierarchy.MaxDepth; depth++)
            {
                query = IncludePath(query, database.Mapper, parts);
                parts.Add(navigation);
            }
        }

        return query;
    }

    private static IReadOnlyList<string> MemberPath(LambdaExpression expression, string path)
    {
        if (expression is not null)
        {
            var body = expression.Body;
            while (body is UnaryExpression unary) { body = unary.Operand; }

            var parts = new Stack<string>();
            while (body is MemberExpression member) { parts.Push(member.Member.Name); body = member.Expression; }

            if (body is not ParameterExpression || parts.Count == 0)
            {
                throw new LiteDbProviderException(new InvalidInputError("An include must select mapped members."));
            }

            return parts.ToArray();
        }

        if (string.IsNullOrWhiteSpace(path)) { throw new LiteDbProviderException(new InvalidInputError("An include path is required.")); }

        return path.Split('.');
    }

    private static ILiteQueryable<TEntity> IncludePath(ILiteQueryable<TEntity> query, BsonMapper mapper, IEnumerable<string> parts)
    {
        var type = typeof(TEntity);
        var path = "$";
        foreach (var part in parts)
        {
            var parameter = Expression.Parameter(type, "navigation");
            var member = Expression.PropertyOrField(parameter, part);
            var lambda = Expression.Lambda(Expression.Convert(member, typeof(object)), parameter);
            // Resolve each segment through the public mapper so aliases and DbRef mappings are preserved.
            var method = typeof(BsonMapper).GetMethod(nameof(BsonMapper.GetExpression))!.MakeGenericMethod(type, typeof(object));
            var segment = ((BsonExpression)method.Invoke(mapper, [lambda])!).Source;
            path += segment[1..];
            query = query.Include(BsonExpression.Create(path));
            type = member.Type;
            var sequence = type.GetInterfaces().Append(type).FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (sequence is not null && type != typeof(string)) { path += "[*]"; type = sequence.GenericTypeArguments[0]; }
        }

        return query;
    }

    private static void Validate(IFindOptions<TEntity> options)
    {
        if (options?.Skip < 0 || options?.Take <= 0)
        {
            throw new LiteDbProviderException(new InvalidInputError("Skip must be nonnegative and Take must be positive."));
        }

        if (options?.Hierarchy is { MaxDepth: < 1 or > 32 })
        {
            throw new LiteDbProviderException(new InvalidInputError("Hierarchy depth must be between one and 32."));
        }
    }

    private static List<OrderOption<TEntity>> GetOrders(IFindOptions<TEntity> options)
    {
        var orders = new List<OrderOption<TEntity>>();
        foreach (var order in (options?.Orders ?? []).Prepend(options?.Order).Where(o => o is not null).Distinct())
        {
            if (order.Expression is not null)
            {
                orders.Add(order);
                continue;
            }

            foreach (var text in (order.Ordering ?? "").Split(','))
            {
                var match = Regex.Match(text.Trim(), "^(?<path>[A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)*)(?:\\s+(?<direction>asc|ascending|desc|descending))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    throw new LiteDbProviderException(new InvalidInputError("The order must contain mapped member paths and optional ascending or descending directions."));
                }

                var parameter = Expression.Parameter(typeof(TEntity), "entity");
                Expression body = parameter;
                foreach (var part in match.Groups["path"].Value.Split('.')) { body = Expression.PropertyOrField(body, part); }

                var expression = Expression.Lambda<Func<TEntity, object>>(Expression.Convert(body, typeof(object)), parameter);
                var direction = match.Groups["direction"].Value.StartsWith("desc", StringComparison.OrdinalIgnoreCase) ? OrderDirection.Descending : OrderDirection.Ascending;
                orders.Add(new OrderOption<TEntity>(expression, direction));
            }
        }

        return orders;
    }

    private void CheckLimit(int count)
    {
        if (count > settings.MaxMaterializedRows)
        {
            throw new LiteDbProviderException(new InvalidOperationError($"The query exceeds MaxMaterializedRows ({settings.MaxMaterializedRows}). Use a smaller selection or raise the configured limit."));
        }
    }
}
