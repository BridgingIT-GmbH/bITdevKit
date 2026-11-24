// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Validates selection structure and dimension semantics before provider access or mutation.</summary>
/// <example><code>var result = CacheSelectionValidator.Validate(selection, schemas);</code></example>
public static class CacheSelectionValidator
{
    /// <summary>Checks the bounded grammar, declared kinds/forms, ranges, and explicit-null policy.</summary>
    public static Result Validate(CacheSelection selection, IReadOnlyList<CacheDimensionSchema> schemas)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(selection);
            var schema = (schemas ?? []).ToDictionary(s => CacheIdentityNormalizer.Name(s.Name), StringComparer.OrdinalIgnoreCase);
            var count = 0;
            Check(selection, 1, ref count, schema);
            return Result.Success();
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Validation, exception.Message));
        }
    }

    /// <summary>Validates entry dimensions against the same immutable schema.</summary>
    public static Result ValidateDimensions(IReadOnlyList<CacheDimension> dimensions, IReadOnlyList<CacheDimensionSchema> schemas)
    {
        if (dimensions is null || dimensions.Count > 16)
        {
            return Failure("At most sixteen dimensions are allowed.");
        }

        if (dimensions.Count == 0)
        {
            return Result.Success();
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dimension in dimensions)
        {
            if (dimension is null || !names.Add(dimension.Name))
            {
                return Failure("Dimension names must be unique.");
            }

            var declared = schemas?.FirstOrDefault(s => string.Equals(s.Name, dimension.Name, StringComparison.OrdinalIgnoreCase));
            if (declared is null || declared.Kind != dimension.Kind || declared.Form != dimension.Form ||
                (!declared.AllowNull && dimension.Values.Any(v => v.IsNull)))
            {
                return Failure("A dimension does not match its declared kind, form, or null policy.");
            }
        }

        return Result.Success();
    }

    private static void Check(CacheSelection node, int depth, ref int count, Dictionary<string, CacheDimensionSchema> schema)
    {
        if (node is null || depth > 8 || ++count > 32 || !Enum.IsDefined(node.Kind) ||
            node.Values.Count > 256 || node.Values.Any(v => v is null))
        {
            throw new ArgumentException("A selection exceeds its structural bounds.");
        }

        if (node.Kind is CacheSelectionKind.And or CacheSelectionKind.Or)
        {
            if (node.Name is not null || node.Text is not null || node.Values.Count != 0)
            {
                throw new ArgumentException("Compound selections accept only child selections.");
            }

            foreach (var child in node.Children)
            {
                Check(child, depth + 1, ref count, schema);
            }

            return;
        }

        if (node.Children.Count != 0)
        {
            throw new ArgumentException("Only AND/OR selections accept children.");
        }

        if (node.Kind is CacheSelectionKind.Key or CacheSelectionKind.Prefix or CacheSelectionKind.Pattern)
        {
            CacheIdentityNormalizer.Key(node.Text);
            if (node.Name is not null || node.Values.Count != 0)
            {
                throw new ArgumentException("Key selections accept only text.");
            }

            if (node.Kind == CacheSelectionKind.Pattern)
            {
                _ = new CachePatternMatcher(node.Text);
            }

            return;
        }

        if (node.Text is not null)
        {
            throw new ArgumentException("This selection does not accept text.");
        }

        if (node.Kind == CacheSelectionKind.None)
        {
            if (node.Name is not null || node.Values.Count != 0)
            {
                throw new ArgumentException("An empty selection has no operands.");
            }

            return;
        }

        if (node.Kind is CacheSelectionKind.PublishedRange or CacheSelectionKind.StoredSizeRange)
        {
            if (node.Name is not null)
            {
                throw new ArgumentException("Metadata ranges do not name a dimension.");
            }

            CheckRange(node.Values, node.Kind == CacheSelectionKind.PublishedRange ? CacheDimensionKind.Timestamp : CacheDimensionKind.Integer);
            if (node.Kind == CacheSelectionKind.StoredSizeRange && node.Values[0].CompareTo(CacheDimensionValue.From(0L)) < 0)
            {
                throw new ArgumentException("Stored-byte ranges cannot be negative.");
            }

            return;
        }

        if (node.Name is null || !schema.TryGetValue(node.Name, out var declared))
        {
            throw new ArgumentException("A selection names an undeclared dimension.");
        }

        if (node.Values.Any(v => v.Kind != declared.Kind || (v.IsNull && !declared.AllowNull)))
        {
            throw new ArgumentException("Dimension predicate values must match the declared kind and null policy.");
        }

        var valid = node.Kind switch
        {
            CacheSelectionKind.Present or CacheSelectionKind.Absent => node.Values.Count == 0,
            CacheSelectionKind.Equal => declared.Form == CacheDimensionForm.Scalar && node.Values.Count == 1,
            CacheSelectionKind.Contains => declared.Form == CacheDimensionForm.Set && node.Values.Count == 1,
            CacheSelectionKind.ContainsAny => declared.Form == CacheDimensionForm.Set,
            CacheSelectionKind.Overlap => declared.Form == CacheDimensionForm.Interval,
            _ => false
        };
        if (!valid)
        {
            throw new ArgumentException("A dimension predicate is incompatible with its declared form.");
        }

        if (node.Kind == CacheSelectionKind.Overlap)
        {
            CheckRange(node.Values, declared.Kind);
        }
    }

    private static void CheckRange(IReadOnlyList<CacheDimensionValue> values, CacheDimensionKind kind)
    {
        if (values.Count != 2 || kind is not (CacheDimensionKind.Integer or CacheDimensionKind.Timestamp) ||
            values.Any(v => v.Kind != kind || v.IsNull) || values[0].CompareTo(values[1]) >= 0)
        {
            throw new ArgumentException("Ranges require ordered, non-null Integer or Timestamp endpoints.");
        }
    }

    private static Result Failure(string message) => Result.Failure(new CacheError(CacheErrorCode.Validation, message));
}