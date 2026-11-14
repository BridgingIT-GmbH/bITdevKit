// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Evaluates a validated selection using shared provider-independent semantics and one UTC cutoff.</summary>
/// <example><code>var evaluator = new CacheSelectionEvaluator(selection, schemas, timeProvider.GetUtcNow());</code></example>
public sealed class CacheSelectionEvaluator
{
    private readonly CacheSelection selection;
    private readonly Dictionary<CacheSelection, CachePatternMatcher> patterns = new(ReferenceEqualityComparer.Instance);

    /// <summary>Validates and prepares the selection once for a bounded provider operation.</summary>
    public CacheSelectionEvaluator(CacheSelection selection, IReadOnlyList<CacheDimensionSchema> schemas, DateTimeOffset cutoff)
    {
        var validation = CacheSelectionValidator.Validate(selection, schemas);
        if (validation.IsFailure)
        {
            throw new ArgumentException(validation.Errors.First().Message, nameof(selection));
        }

        this.selection = selection;
        this.Cutoff = cutoff.ToUniversalTime();
        this.Prepare(selection);
    }

    /// <summary>Gets the operation's fixed publication cutoff. Later publications are excluded.</summary>
    public DateTimeOffset Cutoff { get; }

    /// <summary>Evaluates metadata only. Expiration and protected-reader ownership remain provider responsibilities.</summary>
    public bool Matches(CacheEntryMetadata metadata) =>
        metadata is not null && metadata.PublishedAt <= this.Cutoff && this.Matches(this.selection, metadata);

    private void Prepare(CacheSelection node)
    {
        if (node.Kind == CacheSelectionKind.Pattern && !this.patterns.ContainsKey(node))
        {
            this.patterns.Add(node, new(node.Text));
        }

        foreach (var child in node.Children)
        {
            this.Prepare(child);
        }
    }

    private bool Matches(CacheSelection node, CacheEntryMetadata entry)
    {
        if (node.Kind == CacheSelectionKind.None)
        {
            return false;
        }

        if (node.Kind is CacheSelectionKind.And or CacheSelectionKind.Or)
        {
            return node.Children.Count != 0 && (node.Kind == CacheSelectionKind.And
                ? node.Children.All(n => this.Matches(n, entry)) : node.Children.Any(n => this.Matches(n, entry)));
        }

        if (node.Kind == CacheSelectionKind.Key)
        {
            return string.Equals(node.Text, entry.Identity.Key, StringComparison.OrdinalIgnoreCase);
        }

        if (node.Kind == CacheSelectionKind.Prefix)
        {
            return entry.Identity.Key.StartsWith(node.Text, StringComparison.OrdinalIgnoreCase);
        }

        if (node.Kind == CacheSelectionKind.Pattern)
        {
            return this.patterns[node].IsMatch(entry.Identity.Key);
        }

        if (node.Kind is CacheSelectionKind.PublishedRange or CacheSelectionKind.StoredSizeRange)
        {
            var value = node.Kind == CacheSelectionKind.PublishedRange
                ? CacheDimensionValue.From(entry.PublishedAt) : CacheDimensionValue.From(entry.StoredSizeBytes);
            return value.CompareTo(node.Values[0]) >= 0 && value.CompareTo(node.Values[1]) < 0;
        }

        var dimension = entry.Dimensions.FirstOrDefault(d => string.Equals(d.Name, node.Name, StringComparison.OrdinalIgnoreCase));
        if (node.Kind == CacheSelectionKind.Absent)
        {
            return dimension is null;
        }

        if (dimension is null)
        {
            return false;
        }

        return node.Kind switch
        {
            CacheSelectionKind.Present => true,
            CacheSelectionKind.Equal => dimension.Values[0] == node.Values[0],
            CacheSelectionKind.Contains or CacheSelectionKind.ContainsAny => dimension.Values.Any(node.Values.Contains),
            CacheSelectionKind.Overlap => dimension.Values[0].CompareTo(node.Values[1]) < 0 && node.Values[0].CompareTo(dimension.Values[1]) < 0,
            _ => false
        };
    }
}