// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Application.Storage;
using System.Linq.Expressions;

internal static class DocumentDimensionQueryTranslator
{
    public static async Task<Result<IQueryable<StorageDocument>>> ApplyAsync<TContext>(TContext context, IQueryable<StorageDocument> owners,
        string scope, DocumentDimensionFilter filter, DocumentDimensionCapabilities capabilities,
        DocumentDimensionNameResolver<TContext> resolver, CancellationToken token) where TContext : DbContext
    {
        if (filter is null) return Result<IQueryable<StorageDocument>>.Success(owners);
        var validation = DocumentDimensionFilterValidator.Validate(filter, capabilities);
        if (validation.IsFailure) return Result<IQueryable<StorageDocument>>.Failure(validation);
        var names = Leaves(filter).Select(predicate => predicate.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var resolved = await resolver.ResolveForReadAsync(context, scope, names, token).ConfigureAwait(false);
        if (resolved.IsFailure) return Result<IQueryable<StorageDocument>>.Failure(resolved);
        var candidates = BuildCandidates(context, owners.Select(owner => owner.Id), filter, resolved.Value);
        // Retain the correlation in the unique-owner lookup instead of sorting the scoped parent table.
        var matches = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" && HasValueAnchor(filter)
            ? candidates.SelectMany(id => owners.Where(owner => owner.Id == id).OrderBy(_ => id).Take(1))
            : candidates.Join(owners, id => id, owner => owner.Id, (_, owner) => owner);
        return Result<IQueryable<StorageDocument>>.Success(matches);
    }

    private static bool HasValueAnchor(DocumentDimensionFilter filter)
    {
        if (filter.Predicate is { } predicate)
            return predicate.Operator is DocumentDimensionOperator.Equal or DocumentDimensionOperator.GreaterThan
                or DocumentDimensionOperator.GreaterThanOrEqual or DocumentDimensionOperator.LessThan or DocumentDimensionOperator.LessThanOrEqual;
        return filter.IsAny ? filter.Children.All(HasValueAnchor) : filter.Children.Any(HasValueAnchor);
    }

    private static IQueryable<Guid> BuildCandidates(DbContext context, IQueryable<Guid> universe, DocumentDimensionFilter filter, IReadOnlyDictionary<string, long> names)
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
        var deferred = new List<DocumentDimensionFilter>();
        foreach (var group in children.Where(child => child.Predicate is not null)
            .GroupBy(child => DocumentDimensionValues.CanonicalizeName(child.Predicate.Name), StringComparer.Ordinal))
        {
            if (group.Any(child => child.HasPositiveAnchor))
                positive.Add(BuildConjunction(context, universe, group.Select(child => child.Predicate).ToArray(), names));
            else deferred.Add(DocumentDimensionFilter.All(group.ToArray()));
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

    private static Expression<Func<Guid, bool>> BuildCandidatePredicate(DbContext context, DocumentDimensionFilter filter, IReadOnlyDictionary<string, long> names)
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

        if (!names.TryGetValue(DocumentDimensionValues.CanonicalizeName(predicate.Name), out var nameId))
            return predicate.Operator == DocumentDimensionOperator.Missing ? _ => true : _ => false;
        var rows = context.Set<StorageDocumentDimension>().AsNoTracking().Where(row => row.NameId == nameId);
        if (predicate.Operator == DocumentDimensionOperator.Missing) return ownerId => !rows.Any(row => row.DocumentId == ownerId);
        var matching = ApplyPredicate(rows, predicate);
        return ownerId => matching.Any(row => row.DocumentId == ownerId);
    }

    private static IQueryable<Guid> BuildConjunction(DbContext context, IQueryable<Guid> universe,
        DocumentDimensionPredicate[] predicates, IReadOnlyDictionary<string, long> names)
    {
        var missing = predicates.Any(predicate => predicate.Operator == DocumentDimensionOperator.Missing);
        if (!names.TryGetValue(DocumentDimensionValues.CanonicalizeName(predicates[0].Name), out var nameId))
            return predicates.All(predicate => predicate.Operator == DocumentDimensionOperator.Missing) ? universe : universe.Where(_ => false);
        var values = context.Set<StorageDocumentDimension>().AsNoTracking().Where(row => row.NameId == nameId);
        if (missing)
            return predicates.All(predicate => predicate.Operator == DocumentDimensionOperator.Missing)
                ? universe.Except(values.Select(row => row.DocumentId)) : universe.Where(_ => false);
        foreach (var predicate in predicates) values = ApplyPredicate(values, predicate);
        return values.Select(row => row.DocumentId);
    }

    private static IEnumerable<DocumentDimensionFilter> FlattenAll(DocumentDimensionFilter node) => node.Predicate is not null || node.IsAny
        ? [node] : node.Children.SelectMany(FlattenAll);

    private static IQueryable<StorageDocumentDimension> ApplyPredicate(IQueryable<StorageDocumentDimension> rows, DocumentDimensionPredicate predicate)
    {
        if (predicate.Operator == DocumentDimensionOperator.Exists) return rows;
        if (predicate.Operator == DocumentDimensionOperator.IsNull) return rows.Where(row => row.ValueType == DocumentDimensionValueType.Null);
        if (predicate.Operator == DocumentDimensionOperator.IsNotNull) return rows.Where(row => row.ValueType != DocumentDimensionValueType.Null);
        var encoded = new StorageDocumentDimension();
        DocumentDimensionValueEncoding.Encode(predicate.Operand, encoded);
        var kind = encoded.ValueType;
        rows = rows.Where(row => row.ValueType == kind);
        if (kind == DocumentDimensionValueType.String)
        {
            var operand = encoded.StringValue;
            return predicate.Operator == DocumentDimensionOperator.Equal
                ? rows.Where(row => row.StringValue == operand)
                : rows.Where(row => row.StringValue != null && row.StringValue != operand);
        }

        if (kind == DocumentDimensionValueType.Decimal)
        {
            var operand = encoded.DecimalValue.Value;
            return predicate.Operator switch
            {
                DocumentDimensionOperator.Equal => rows.Where(row => row.DecimalValue == operand),
                DocumentDimensionOperator.NotEqual => rows.Where(row => row.DecimalValue != null && row.DecimalValue != operand),
                DocumentDimensionOperator.GreaterThan => rows.Where(row => row.DecimalValue > operand),
                DocumentDimensionOperator.GreaterThanOrEqual => rows.Where(row => row.DecimalValue >= operand),
                DocumentDimensionOperator.LessThan => rows.Where(row => row.DecimalValue < operand),
                DocumentDimensionOperator.LessThanOrEqual => rows.Where(row => row.DecimalValue <= operand),
                _ => throw new InvalidOperationException("Unsupported Dimension decimal operator.")
            };
        }

        var integral = encoded.IntegralValue.Value;
        return predicate.Operator switch
        {
            DocumentDimensionOperator.Equal => rows.Where(row => row.IntegralValue == integral),
            DocumentDimensionOperator.NotEqual => rows.Where(row => row.IntegralValue != null && row.IntegralValue != integral),
            DocumentDimensionOperator.GreaterThan => rows.Where(row => row.IntegralValue > integral),
            DocumentDimensionOperator.GreaterThanOrEqual => rows.Where(row => row.IntegralValue >= integral),
            DocumentDimensionOperator.LessThan => rows.Where(row => row.IntegralValue < integral),
            DocumentDimensionOperator.LessThanOrEqual => rows.Where(row => row.IntegralValue <= integral),
            _ => throw new InvalidOperationException("Unsupported Dimension integral operator.")
        };
    }

    private static IEnumerable<DocumentDimensionPredicate> Leaves(DocumentDimensionFilter filter) => filter.Predicate is { } predicate
        ? [predicate] : filter.Children.SelectMany(Leaves);
}
