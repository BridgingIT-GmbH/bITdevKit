// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Application.Storage;
using System.Linq.Expressions;

/// <summary>Translates blob dimension filters into composable EF queries over indexed dimension rows.</summary>
/// <example>
/// <code>
/// var candidates = BlobDimensionQueryTranslator.BuildCandidates(context, ownerIds, filter, resolvedNames);
/// </code>
/// </example>
public static class BlobDimensionQueryTranslator
{
    /// <summary>Validates a filter, resolves its dimension names, and builds a query for the matching blob rows.</summary>
    /// <typeparam name="TContext">The relational context type that maps blobs and their dimension tables.</typeparam>
    /// <param name="context">The context used to resolve names and query the dimension rows.</param>
    /// <param name="owners">The blob rows already restricted to the requested container.</param>
    /// <param name="scope">The container name used to resolve dimension names.</param>
    /// <param name="filter">The filter to apply. A null filter returns the supplied owner query.</param>
    /// <param name="capabilities">The dimension operations supported by the current provider.</param>
    /// <param name="resolver">The resolver for dimension names in the current store.</param>
    /// <param name="token">Cancels dimension name resolution.</param>
    /// <returns>A composable owner query on success or the validation or name resolution errors on failure.</returns>
    /// <example>
    /// <code>
    /// var result = await BlobDimensionQueryTranslator.ApplyAsync(
    ///     context, context.StorageBlobs.Where(row =&gt; row.Container == "reports"), "reports",
    ///     filter, capabilities, resolver, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<Result<IQueryable<StorageBlob>>> ApplyAsync<TContext>(TContext context, IQueryable<StorageBlob> owners,
        string scope, BlobDimensionFilter filter, BlobDimensionCapabilities capabilities,
        BlobDimensionNameResolver<TContext> resolver, CancellationToken token) where TContext : DbContext
    {
        if (filter is null) return Result<IQueryable<StorageBlob>>.Success(owners);
        var validation = BlobDimensionFilterValidator.Validate(filter, capabilities);
        if (validation.IsFailure) return Result<IQueryable<StorageBlob>>.Failure(validation);
        var names = Leaves(filter).Select(predicate => predicate.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var resolved = await resolver.ResolveForReadAsync(context, scope, names, token).ConfigureAwait(false);
        if (resolved.IsFailure) return Result<IQueryable<StorageBlob>>.Failure(resolved);
        var candidates = BuildCandidates(context, owners.Select(owner => owner.Id), filter, resolved.Value);
        // Retain the correlation in the unique-owner lookup instead of sorting the scoped parent table.
        var matches = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" && HasValueAnchor(filter)
            ? candidates.SelectMany(id => owners.Where(owner => owner.Id == id).OrderBy(_ => id).Take(1))
            : candidates.Join(owners, id => id, owner => owner.Id, (_, owner) => owner);
        return Result<IQueryable<StorageBlob>>.Success(matches);
    }

    private static bool HasValueAnchor(BlobDimensionFilter filter)
    {
        if (filter.Predicate is { } predicate)
            return predicate.Operator is BlobDimensionOperator.Equal or BlobDimensionOperator.GreaterThan
                or BlobDimensionOperator.GreaterThanOrEqual or BlobDimensionOperator.LessThan or BlobDimensionOperator.LessThanOrEqual;
        return filter.IsAny ? filter.Children.All(HasValueAnchor) : filter.Children.Any(HasValueAnchor);
    }

    /// <summary>Builds a query for matching blob IDs using indexed dimension rows and resolved names.</summary>
    /// <param name="context">The relational context that maps the blob dimension tables.</param>
    /// <param name="universe">The blob IDs in scope, used for filter branches without an indexed match to start from.</param>
    /// <param name="filter">A filter already validated against the provider's dimension capabilities.</param>
    /// <param name="names">The dictionary IDs for the canonical dimension names found in scope.</param>
    /// <returns>A deferred query for the candidate IDs. Join it to the scoped owner query before returning blobs.</returns>
    /// <example>
    /// <code>
    /// var ownerIds = context.StorageBlobs.Where(row =&gt; row.Container == "reports").Select(row =&gt; row.Id);
    /// var candidates = BlobDimensionQueryTranslator.BuildCandidates(context, ownerIds, filter, resolvedNames);
    /// </code>
    /// </example>
    public static IQueryable<Guid> BuildCandidates(DbContext context, IQueryable<Guid> universe, BlobDimensionFilter filter, IReadOnlyDictionary<string, long> names)
    {
        if (filter.Predicate is { } predicate) return BuildConjunction(context, universe, [predicate], names);
        if (filter.IsAny)
        {
            // A branch without its own anchor is evaluated against the supplied key-bounded/full-scan universe.
            // An enclosing AND defers such a group until its indexed positive candidates are known.
            if (filter.Children.Any(child => !child.HasPositiveAnchor))
                return universe.Where(BuildCandidatePredicate(context, filter, names));
            var branches = filter.Children.Select(child => BuildCandidates(context, universe, child, names)).ToArray();
            var union = branches[0];
            foreach (var branch in branches.Skip(1)) union = union.Union(branch);
            return union;
        }

        var children = FlattenAll(filter).ToArray();
        var positive = new List<IQueryable<Guid>>();
        var deferred = new List<BlobDimensionFilter>();
        foreach (var group in children.Where(child => child.Predicate is not null)
            .GroupBy(child => BlobDimensionValues.CanonicalizeName(child.Predicate.Name), StringComparer.Ordinal))
        {
            if (group.Any(child => child.HasPositiveAnchor))
                positive.Add(BuildConjunction(context, universe, group.Select(child => child.Predicate).ToArray(), names));
            else deferred.Add(BlobDimensionFilter.All(group.ToArray()));
        }

        foreach (var child in children.Where(child => child.Predicate is null))
        {
            if (child.HasPositiveAnchor) positive.Add(BuildCandidates(context, universe, child, names));
            else deferred.Add(child);
        }

        var candidates = positive.Count == 0 ? universe : positive[0];
        foreach (var next in positive.Skip(1)) candidates = candidates.Intersect(next);
        // Negative-only terms probe only the selected candidate IDs. Unknown Missing becomes true,
        // rather than an INTERSECT that enumerates every parent in the scope.
        foreach (var child in deferred) candidates = candidates.Where(BuildCandidatePredicate(context, child, names));
        return candidates;
    }

    private static Expression<Func<Guid, bool>> BuildCandidatePredicate(DbContext context, BlobDimensionFilter filter, IReadOnlyDictionary<string, long> names)
    {
        if (filter.Predicate is not { } predicate)
        {
            var builder = new PredicateBuilder<Guid>();
            if (filter.IsAny) builder.Add(_ => false);
            foreach (var child in filter.Children)
            {
                var expression = BuildCandidatePredicate(context, child, names);
                if (filter.IsAny) builder.Or(expression);
                else builder.Add(expression);
            }

            return builder.BuildExpression();
        }

        if (!names.TryGetValue(BlobDimensionValues.CanonicalizeName(predicate.Name), out var nameId))
            return predicate.Operator == BlobDimensionOperator.Missing ? _ => true : _ => false;
        var rows = context.Set<StorageBlobDimension>().AsNoTracking().Where(row => row.NameId == nameId);
        if (predicate.Operator == BlobDimensionOperator.Missing) return ownerId => !rows.Any(row => row.BlobId == ownerId);
        var matching = ApplyPredicate(rows, predicate);
        return ownerId => matching.Any(row => row.BlobId == ownerId);
    }

    private static IQueryable<Guid> BuildConjunction(DbContext context, IQueryable<Guid> universe,
        BlobDimensionPredicate[] predicates, IReadOnlyDictionary<string, long> names)
    {
        var missing = predicates.Any(predicate => predicate.Operator == BlobDimensionOperator.Missing);
        if (!names.TryGetValue(BlobDimensionValues.CanonicalizeName(predicates[0].Name), out var nameId))
            return predicates.All(predicate => predicate.Operator == BlobDimensionOperator.Missing) ? universe : universe.Where(_ => false);
        var values = context.Set<StorageBlobDimension>().AsNoTracking().Where(row => row.NameId == nameId);
        if (missing)
            return predicates.All(predicate => predicate.Operator == BlobDimensionOperator.Missing)
                ? universe.Except(values.Select(row => row.BlobId)) : universe.Where(_ => false);
        foreach (var predicate in predicates) values = ApplyPredicate(values, predicate);
        return values.Select(row => row.BlobId);
    }

    private static IEnumerable<BlobDimensionFilter> FlattenAll(BlobDimensionFilter node) => node.Predicate is not null || node.IsAny
        ? [node] : node.Children.SelectMany(FlattenAll);

    private static IQueryable<StorageBlobDimension> ApplyPredicate(IQueryable<StorageBlobDimension> rows, BlobDimensionPredicate predicate)
    {
        if (predicate.Operator == BlobDimensionOperator.Exists) return rows;
        if (predicate.Operator == BlobDimensionOperator.IsNull) return rows.Where(row => row.ValueType == BlobDimensionValueType.Null);
        if (predicate.Operator == BlobDimensionOperator.IsNotNull) return rows.Where(row => row.ValueType != BlobDimensionValueType.Null);
        var encoded = new StorageBlobDimension();
        BlobDimensionValueEncoding.Encode(predicate.Operand, encoded);
        var kind = encoded.ValueType;
        rows = rows.Where(row => row.ValueType == kind);
        if (kind == BlobDimensionValueType.String)
        {
            var operand = encoded.StringValue;
            return predicate.Operator == BlobDimensionOperator.Equal
                ? rows.Where(row => row.StringValue == operand)
                : rows.Where(row => row.StringValue != null && row.StringValue != operand);
        }

        if (kind == BlobDimensionValueType.Decimal)
        {
            var operand = encoded.DecimalValue.Value;
            return predicate.Operator switch
            {
                BlobDimensionOperator.Equal => rows.Where(row => row.DecimalValue == operand),
                BlobDimensionOperator.NotEqual => rows.Where(row => row.DecimalValue != null && row.DecimalValue != operand),
                BlobDimensionOperator.GreaterThan => rows.Where(row => row.DecimalValue > operand),
                BlobDimensionOperator.GreaterThanOrEqual => rows.Where(row => row.DecimalValue >= operand),
                BlobDimensionOperator.LessThan => rows.Where(row => row.DecimalValue < operand),
                BlobDimensionOperator.LessThanOrEqual => rows.Where(row => row.DecimalValue <= operand),
                _ => throw new InvalidOperationException("Unsupported Dimension decimal operator.")
            };
        }

        var integral = encoded.IntegralValue.Value;
        return predicate.Operator switch
        {
            BlobDimensionOperator.Equal => rows.Where(row => row.IntegralValue == integral),
            BlobDimensionOperator.NotEqual => rows.Where(row => row.IntegralValue != null && row.IntegralValue != integral),
            BlobDimensionOperator.GreaterThan => rows.Where(row => row.IntegralValue > integral),
            BlobDimensionOperator.GreaterThanOrEqual => rows.Where(row => row.IntegralValue >= integral),
            BlobDimensionOperator.LessThan => rows.Where(row => row.IntegralValue < integral),
            BlobDimensionOperator.LessThanOrEqual => rows.Where(row => row.IntegralValue <= integral),
            _ => throw new InvalidOperationException("Unsupported Dimension integral operator.")
        };
    }

    private static IEnumerable<BlobDimensionPredicate> Leaves(BlobDimensionFilter filter) => filter.Predicate is { } predicate
        ? [predicate] : filter.Children.SelectMany(Leaves);
}
