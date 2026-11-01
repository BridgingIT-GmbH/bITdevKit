// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Application-facing scoped audit reads independent of HTTP, dashboard, and EF.</summary>
/// <example><code>var page = await queries.SearchAsync(new FilterModel());</code></example>
public interface IAuditQueryService
{
    /// <summary>Searches approved fields under one trusted host decision and bounded absolute range.</summary>
    /// <example><code>var page = await queries.SearchAsync(filter);</code></example>
    Task<ResultPaged<AuditRecordSummary>> SearchAsync(FilterModel filter = null, CancellationToken cancellationToken = default);

    /// <summary>Reads exact retained identity, including archives, after scope and original-source verification.</summary>
    /// <example><code>var details = await queries.FindAsync(auditId);</code></example>
    Task<Result<AuditRecordDetails>> FindAsync(Guid auditId, CancellationToken cancellationToken = default);

    /// <summary>Finds explicit causal links only after the anchor passes trusted scope.</summary>
    /// <example><code>var related = await queries.FindRelatedAsync(auditId, filter);</code></example>
    Task<ResultPaged<AuditRecordSummary>> FindRelatedAsync(Guid auditId, FilterModel filter = null, CancellationToken cancellationToken = default);

    /// <summary>Counts an approved root grouping under the same scoped selection.</summary>
    /// <example><code>var counts = await queries.AggregateAsync(filter, "Outcome");</code></example>
    Task<Result<ImmutableArray<AuditAggregateItem>>> AggregateAsync(FilterModel filter, string field, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>Returns bounded scoped suggestions from an approved string field.</summary>
    /// <example><code>var keys = await queries.FindFacetsAsync(filter, "Key", "Customer");</code></example>
    Task<Result<ImmutableArray<string>>> FindFacetsAsync(FilterModel filter, string field, string prefix = null, int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>Returns one row per execution matching the mandatory exact typed resource.</summary>
    /// <example><code>var history = await queries.GetResourceHistoryAsync(new("Customer", "42"), filter);</code></example>
    Task<ResultPaged<AuditResourceHistoryItem>> GetResourceHistoryAsync(AuditResourceReference resource, FilterModel filter = null, CancellationToken cancellationToken = default);

    /// <summary>Prepares a validated immutable scoped selection for bounded provider extraction.</summary>
    /// <example><code>var plan = await queries.PrepareAsync(filter);</code></example>
    Task<Result<AuditSearchPlan>> PrepareAsync(FilterModel filter = null, CancellationToken cancellationToken = default);

    /// <summary>Rebuilds current host scope using a pinned selection clock for a protected export continuation.</summary>
    /// <example><code>var plan = await queries.PrepareAtAsync(filter, originalAsOfUtc, cancellationToken);</code></example>
    Task<Result<AuditSearchPlan>> PrepareAtAsync(FilterModel filter, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(Result<AuditSearchPlan>.Failure(new AuditError("AuditCapabilityUnsupported")));
}
