// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Globalization;
using System.Text;
using BridgingIT.DevKit.Application.Caching;

// Every driver emits at most one candidate per current version. The shared evaluator applies the
// complete canonical predicate; unions and any-member predicates cannot duplicate entries across pages.
/// <summary>Selects a bounded index range and its candidate-filtering rank for one validated metadata selection.</summary>
/// <example><code>var plan = CacheFileSelectionPlan.Build(scope, CacheSelection.Prefix(&quot;product:&quot;));</code></example>
public sealed record CacheFileSelectionPlan(CacheIndexRange Range, bool Primary, int Rank)
{
    /// <summary>Chooses an indexed candidate range for supported predicates and a primary-entry scan for broader or compound selections.</summary>
    public static CacheFileSelectionPlan Build(CacheEntryIdentity scope, CacheSelection selection) => selection.Kind switch
    {
        CacheSelectionKind.Key => new(CacheMetadataIndex.PrefixRange(CacheMetadataIndex.LogicalPrefix(
            new(scope.Namespace, scope.Collection, scope.ContractVersion, selection.Text))), false, 0),
        CacheSelectionKind.Prefix => new(CacheMetadataIndex.KeyPrefixRange(selection.Text), false, 2),
        CacheSelectionKind.Pattern => Pattern(selection.Text),
        CacheSelectionKind.Equal => new(CacheMetadataIndex.ValueRange(selection.Name, CacheDimensionForm.Scalar, selection.Values[0]), false, 1),
        CacheSelectionKind.Contains => new(CacheMetadataIndex.ValueRange(selection.Name, CacheDimensionForm.Set, selection.Values[0]), false, 1),
        CacheSelectionKind.ContainsAny or CacheSelectionKind.Present => new(CacheMetadataIndex.PresenceNameRange(selection.Name), false, 4),
        CacheSelectionKind.Overlap => new(CacheMetadataIndex.IntervalRange(selection.Name, selection.Values[0].Kind, Number(selection.Values[1])), false, 3),
        CacheSelectionKind.PublishedRange => new(CacheMetadataIndex.OrderedRange(CacheMetadataIndex.Published, Number(selection.Values[0]), Number(selection.Values[1])), false, 3),
        CacheSelectionKind.StoredSizeRange => new(CacheMetadataIndex.OrderedRange(CacheMetadataIndex.StoredSize, Number(selection.Values[0]), Number(selection.Values[1])), false, 3),
        CacheSelectionKind.And when selection.Children.Count != 0 => selection.Children.Select(c => Build(scope, c)).MinBy(p => p.Rank),
        _ => new(CacheMetadataIndex.PrefixRange(new byte[] { CacheMetadataIndex.Entry }), true, 10)
    };

    private static CacheFileSelectionPlan Pattern(string pattern)
    {
        var literal = new StringBuilder();
        var escaped = false;
        foreach (var character in pattern)
        {
            if (escaped) { literal.Append(character); escaped = false; }
            else if (character == '\\') { escaped = true; }
            else if (character is '*' or '?') { break; }
            else { literal.Append(character); }
        }

        return literal.Length == 0 ? new(CacheMetadataIndex.PrefixRange(new byte[] { CacheMetadataIndex.Entry }), true, 10) :
            new(CacheMetadataIndex.KeyPrefixRange(literal.ToString()), false, 2);
    }

    private static long Number(CacheDimensionValue value) => long.Parse(value.CanonicalValue, CultureInfo.InvariantCulture);
}