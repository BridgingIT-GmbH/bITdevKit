// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;

/// <summary>Combines storage criteria as SQL candidate sets and applies keyset paging.</summary>
public static class EntityFrameworkStorageFilterQuery
{
    /// <summary>Enumerates typed Dimension leaves before resolving their indexed names.</summary>
    public static IEnumerable<TDimension> Dimensions<TDimension>(StorageFilterCriterion<TDimension> criterion) where TDimension : class =>
        criterion?.Children is { } children ? children.SelectMany(Dimensions) : criterion?.Dimension is { } dimension ? [dimension] : [];

    /// <summary>Applies key and Dimension predicates without materializing an OR union after an AND has narrowed the candidates.</summary>
    public static IQueryable<TEntity> Filter<TEntity, TId, TDimension>(IQueryable<TEntity> owners,
        StorageFilterCriterion<TDimension> criterion, Expression<Func<TEntity, TId>> id,
        Func<StorageFilterCriterion<TDimension>, Expression<Func<TEntity, bool>>> matchKey,
        Func<IQueryable<TId>, TDimension, IQueryable<TId>> matchDimension) where TDimension : class
    {
        return criterion is null ? owners : Matches(criterion);

        IQueryable<TId> Keys(StorageFilterCriterion<TDimension> node) => node.Dimension is { } dimension
            ? matchDimension(owners.Select(id), dimension)
            : Matches(node).Select(id);

        IQueryable<TEntity> Matches(StorageFilterCriterion<TDimension> node)
        {
            if (node.Children is null)
            {
                if (node.Dimension is not { } dimension) return owners.Where(matchKey(node));
                return matchDimension(owners.Select(id), dimension).Join(owners, key => key, id, (_, owner) => owner);
            }

            if (node.IsAny)
            {
                if (!node.IsAnchored) return owners.Where(MatchPredicate(node));

                var keys = Keys(node.Children[0]);
                foreach (var child in node.Children.Skip(1))
                    keys = keys.Union(Keys(child));
                return keys.Join(owners, key => key, id, (_, owner) => owner);
            }

            // Prefer an exact key or an indexed Dimension as the first candidate set.
            var driver = node.Children.Where(child => child.IsAnchored).OrderBy(child => child switch
            {
                { Field: not null, Operator: FilterOperator.Equal } => 0,
                { Dimension: not null } => 1,
                { Field: not null } => 2,
                _ => 3
            }).FirstOrDefault();
            var candidates = driver is null ? owners : Matches(driver);

            foreach (var child in node.Children.Where(child => child != driver))
                candidates = candidates.Where(MatchPredicate(child));
            return candidates;
        }

        Expression<Func<TEntity, bool>> MatchPredicate(StorageFilterCriterion<TDimension> node)
        {
            if (node.Dimension is { } dimension)
            {
                // Probe the indexed Dimension for the current owner inside a deferred branch.
                var keys = matchDimension(owners.Select(id), dimension);
                var key = Expression.Parameter(typeof(TId), "key");
                var sameOwner = Expression.Lambda<Func<TId, bool>>(Expression.Equal(key, id.Body), key);
                var exists = Expression.Call(typeof(Queryable), nameof(Queryable.Any), [typeof(TId)], keys.Expression, sameOwner);
                return Expression.Lambda<Func<TEntity, bool>>(exists, id.Parameters);
            }

            if (node.Children is null) return matchKey(node);

            var predicate = new PredicateBuilder<TEntity>();
            if (node.IsAny) predicate.Add(_ => false);
            foreach (var child in node.Children)
            {
                var match = MatchPredicate(child);
                if (node.IsAny) predicate.Or(match);
                else predicate.Add(match);
            }

            return predicate.BuildExpression();
        }
    }

    /// <summary>Seeks past the last key tuple and applies the same order used to create that tuple.</summary>
    public static IQueryable<TEntity> Seek<TEntity>(IQueryable<TEntity> rows, IReadOnlyList<StorageFilterOrder> order, IReadOnlyList<string> position)
    {
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        if (position is not null)
        {
            Expression after = Expression.Constant(false);
            for (var index = order.Count - 1; index >= 0; index--)
            {
                var field = Expression.Property(entity, order[index].Field);
                var value = position[index];
                Expression<Func<string>> parameter = () => value;
                var compare = Expression.Call(typeof(string), nameof(string.Compare), Type.EmptyTypes, field, parameter.Body);
                var comparison = order[index].Descending ? ExpressionType.LessThan : ExpressionType.GreaterThan;
                after = Expression.OrElse(Expression.MakeBinary(comparison, compare, Expression.Constant(0)),
                    Expression.AndAlso(Expression.Equal(field, parameter.Body), after));
            }

            rows = rows.Where(Expression.Lambda<Func<TEntity, bool>>(after, entity));
        }

        IOrderedQueryable<TEntity> ordered = null;
        foreach (var term in order)
        {
            var selector = Expression.Lambda<Func<TEntity, string>>(Expression.Property(entity, term.Field), entity);
            ordered = ordered is null
                ? term.Descending ? rows.OrderByDescending(selector) : rows.OrderBy(selector)
                : term.Descending ? ordered.ThenByDescending(selector) : ordered.ThenBy(selector);
        }

        return ordered ?? rows;
    }
}
