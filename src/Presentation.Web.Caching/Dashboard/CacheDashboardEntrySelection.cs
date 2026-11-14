// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using System.Globalization;
using System.Text;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.AspNetCore.Http;

/// <summary>Builds a bounded entry selection from the dashboard's key prefix and typed dimension controls.</summary>
/// <example><code>var selection = CacheDashboardEntrySelection.Parse(context.Request.Query, collection.Options.Dimensions);</code></example>
public static class CacheDashboardEntrySelection
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Combines an optional literal key prefix with one predicate over a declared collection dimension.</summary>
    /// <param name="query">The current entry-filter query, including optional continuation controls.</param>
    /// <param name="schemas">The selected collection's immutable dimension declarations.</param>
    /// <returns>The combined selection, or null when neither filter was supplied.</returns>
    /// <exception cref="ArgumentException">A filter is repeated, oversized, undeclared, or incompatible with its dimension.</exception>
    /// <remarks>Timestamp inputs without an offset are interpreted as UTC. Explicit null uses its own operator; an empty String operand remains an empty String.</remarks>
    /// <example><code>// ?prefix=product:&amp;dimension=region&amp;dimensionOperator=equal&amp;dimensionValue=west
    /// var selection = CacheDashboardEntrySelection.Parse(request.Query, schemas);</code></example>
    public static CacheSelection Parse(IQueryCollection query, IReadOnlyList<CacheDimensionSchema> schemas)
    {
        ArgumentNullException.ThrowIfNull(query);
        var prefix = Read(query, "prefix", 4096);
        var name = Read(query, "dimension", 128);
        var operation = Read(query, "dimensionOperator", 32).ToLowerInvariant();
        var value = Read(query, "dimensionValue", 4096);
        var upper = Read(query, "dimensionUpper", 4096);
        var prefixSelection = prefix.Length == 0 ? null : CacheSelection.Prefix(prefix);
        if (name.Length == 0)
        {
            if (query.ContainsKey("dimensionOperator") || query.ContainsKey("dimensionValue") || query.ContainsKey("dimensionUpper"))
            {
                throw new ArgumentException("Select a declared dimension before supplying its condition or values.");
            }

            return prefixSelection;
        }

        var normalized = CacheIdentityNormalizer.Name(name);
        var schema = schemas?.FirstOrDefault(item => string.Equals(item.Name, normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Select a declared collection dimension.");
        if (operation.Length == 0)
        {
            operation = schema.Form switch
            {
                CacheDimensionForm.Scalar => "equal",
                CacheDimensionForm.Set => "contains",
                CacheDimensionForm.Interval => "overlap",
                _ => throw new ArgumentException("The dimension form is unsupported.")
            };
        }

        var kind = operation switch
        {
            "equal" or "null" => CacheSelectionKind.Equal,
            "contains" or "contains-null" => CacheSelectionKind.Contains,
            "overlap" => CacheSelectionKind.Overlap,
            "present" => CacheSelectionKind.Present,
            "absent" => CacheSelectionKind.Absent,
            _ => throw new ArgumentException("Select a supported dimension condition.")
        };
        var operands = operation is "equal" or "contains" ? 1 : operation == "overlap" ? 2 : 0;
        if ((operands > 0 && !query.ContainsKey("dimensionValue")) || (operands == 2 && !query.ContainsKey("dimensionUpper")))
        {
            throw new ArgumentException("Enter the required dimension value or interval endpoints.");
        }

        if ((operands == 0 && query.ContainsKey("dimensionValue")) || (operands < 2 && query.ContainsKey("dimensionUpper")))
        {
            throw new ArgumentException("The selected dimension condition does not accept these operands.");
        }

        var values = operation is "null" or "contains-null"
            ? new[] { CacheDimensionValue.From(null, schema.Kind) }
            : operands switch
            {
                1 => new[] { ParseValue(value, schema.Kind) },
                2 => new[] { ParseValue(value, schema.Kind), ParseValue(upper, schema.Kind) },
                _ => Array.Empty<CacheDimensionValue>()
            };
        var dimension = new CacheSelection(kind, schema.Name, values: values);
        var selection = prefixSelection is null ? dimension : CacheSelection.And(prefixSelection, dimension);
        var validation = CacheSelectionValidator.Validate(selection, schemas);
        if (validation.IsFailure)
        {
            throw new ArgumentException(validation.Errors.First().Message);
        }

        return selection;
    }

    private static string Read(IQueryCollection query, string field, int maximumBytes)
    {
        var values = query[field];
        if (values.Count > 1)
        {
            throw new ArgumentException("Supply each entry filter only once.");
        }

        var value = values.ToString();
        if (value.Length > maximumBytes || StrictUtf8.GetByteCount(value) > maximumBytes || value.Contains('\0'))
        {
            throw new ArgumentException("An entry filter exceeds its supported input bounds.");
        }

        return value;
    }

    private static CacheDimensionValue ParseValue(string value, CacheDimensionKind kind) => kind switch
    {
        CacheDimensionKind.String => CacheDimensionValue.From(value),
        CacheDimensionKind.Boolean when bool.TryParse(value, out var flag) => CacheDimensionValue.From(flag),
        CacheDimensionKind.Integer when long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) => CacheDimensionValue.From(number),
        CacheDimensionKind.Identifier when Guid.TryParse(value, out var identifier) => CacheDimensionValue.From(identifier),
        CacheDimensionKind.Timestamp when DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp) => CacheDimensionValue.From(timestamp),
        _ => throw new ArgumentException($"Enter a valid {kind} dimension value.")
    };
}