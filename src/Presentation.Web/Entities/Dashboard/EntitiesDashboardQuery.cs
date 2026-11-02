// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using Microsoft.AspNetCore.Http;

/// <summary>
/// The parsed query-string state of the Entities dashboard (entity, filters, match mode, quick search, sort, batch, selected id and columns).
/// Values are raw client input and are validated against the entity descriptors by <see cref="EntitiesDashboardFilterBuilder"/>.
/// </summary>
/// <param name="Entity">The entity key (<c>entity</c>).</param>
/// <param name="Source">The source name (<c>source</c>), or null for the default source.</param>
/// <param name="Filters">The raw filter conditions (<c>f=path~op~value</c>, repeated).</param>
/// <param name="Match">The match mode (<c>match</c>), <c>and</c> or <c>or</c>.</param>
/// <param name="QuickSearch">The quick search text (<c>q</c>).</param>
/// <param name="Sort">The sort property path (<c>sort</c>).</param>
/// <param name="Direction">The sort direction (<c>dir</c>), <c>asc</c> or <c>desc</c>.</param>
/// <param name="Batch">The 1-based batch number (<c>batch</c>).</param>
/// <param name="Id">The selected row id (<c>id</c>).</param>
/// <param name="Columns">The visible column paths (<c>columns</c>, comma separated).</param>
/// <param name="PageSize">The requested rows per batch (<c>size</c>), or 0 for the configured batch size.</param>
internal sealed record EntitiesDashboardQuery(
    string Entity,
    string Source,
    IReadOnlyList<string> Filters,
    string Match,
    string QuickSearch,
    string Sort,
    string Direction,
    int Batch,
    string Id,
    IReadOnlyList<string> Columns,
    int PageSize = 0)
{
    /// <summary>The maximum number of filter conditions accepted per request.</summary>
    public const int MaxConditions = 20;

    /// <summary>Gets a value indicating whether conditions are joined with OR.</summary>
    public bool MatchAny => string.Equals(this.Match, "or", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether the sort direction is descending.</summary>
    public bool Descending => string.Equals(this.Direction, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>Parses the query-string state from a request.</summary>
    /// <param name="query">The query collection.</param>
    /// <returns>The parsed query; missing keys become null or defaults.</returns>
    public static EntitiesDashboardQuery From(IQueryCollection query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var batch = int.TryParse(query["batch"].ToString(), out var parsed) && parsed > 0 ? parsed : 1;
        var columns = query["columns"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new EntitiesDashboardQuery(
            Clean(query["entity"]),
            Clean(query["source"]),
            query["f"].Where(f => !string.IsNullOrWhiteSpace(f)).ToList(),
            Clean(query["match"]),
            Clean(query["q"]),
            Clean(query["sort"]),
            Clean(query["dir"]),
            batch,
            Clean(query["id"]),
            columns,
            int.TryParse(query["size"].ToString(), out var size) && size > 0 ? size : 0);
    }

    private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
