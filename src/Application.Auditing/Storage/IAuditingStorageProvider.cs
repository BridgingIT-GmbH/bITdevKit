// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Accepts frozen audit documents with explicit delivery receipts.</summary>
/// <example><code>var receipt = await provider.AppendAsync(record, cancellationToken);</code></example>
public interface IAuditingStorageProvider
{
    /// <summary>Admits a frozen document, distinguishing confirmed persistence from queue acceptance.</summary>
    /// <example><code>var receipt = await provider.AppendAsync(record);</code></example>
    Task<Result<AuditAppendReceipt>> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>Reads an original complete document or a typed not-found/failure.</summary>
    /// <example><code>var record = await provider.FindAsync(auditId);</code></example>
    Task<Result<AuditRecord>> FindAsync(Guid auditId, CancellationToken cancellationToken = default);

    /// <summary>Confirms all covered admissions through the invocation boundary.</summary>
    /// <example><code>var flush = await provider.FlushAsync(cancellationToken);</code></example>
    Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Declares actual delivery, query and extraction guarantees.</summary>
    /// <example><code>var capabilities = provider.Capabilities;</code></example>
    AuditProviderCapabilities Capabilities => new();

    /// <summary>Optional already-observed process-local receipt counters. This getter performs no I/O and never flushes pending work.</summary>
    /// <example><code>var observed = provider.DeliveryStatus;</code></example>
    AuditFlushResult DeliveryStatus => null;

    /// <summary>Checks provider availability without writing evidence, flushing pending work, or creating schema.</summary>
    /// <remarks>Custom providers must implement this probe to participate in auditing health checks.</remarks>
    /// <example><code>var readiness = await provider.CheckReadinessAsync(cancellationToken);</code></example>
    Task<Result<AuditProviderCapabilities>> CheckReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditProviderCapabilities>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Applies mandatory trusted constraints in the provider before exposing an original graph.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.FindAsync(id, trustedPredicate);</code></example>
    Task<Result<AuditRecord>> FindAsync(Guid auditId, AuditPredicate scope, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditRecord>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Reads original evidence and lifecycle state under one scoped provider snapshot.</summary>
    /// <example><code>var stored = await provider.FindStoredAsync(id, trustedPredicate);</code></example>
    Task<Result<AuditExtractionItem>> FindStoredAsync(Guid auditId, AuditPredicate scope, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditExtractionItem>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Applies all predicates before exact count, ordering and page selection.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.SearchAsync(plan);</code></example>
    Task<ResultPaged<AuditRecordSummary>> SearchAsync(AuditSearchPlan plan, CancellationToken cancellationToken = default) => Task.FromResult(ResultPaged<AuditRecordSummary>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Selects exact typed-resource history under the effective mandatory scope.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.GetResourceHistoryAsync(plan, resource);</code></example>
    Task<ResultPaged<AuditResourceHistoryItem>> GetResourceHistoryAsync(AuditSearchPlan plan, AuditResourceReference resource, CancellationToken cancellationToken = default) => Task.FromResult(ResultPaged<AuditResourceHistoryItem>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Selects bounded explicitly linked executions without correlation inference.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.FindRelatedAsync(query);</code></example>
    Task<ResultPaged<AuditRecordSummary>> FindRelatedAsync(AuditRelatedExecutionsQuery query, CancellationToken cancellationToken = default) => Task.FromResult(ResultPaged<AuditRecordSummary>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Counts approved groups using the same scoped provider selection.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.AggregateAsync(query);</code></example>
    Task<Result<System.Collections.Immutable.ImmutableArray<AuditAggregateItem>>> AggregateAsync(AuditAggregateQuery query, CancellationToken cancellationToken = default) => Task.FromResult(Result<System.Collections.Immutable.ImmutableArray<AuditAggregateItem>>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Returns bounded distinct scoped suggestions from approved fields.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.FindFacetsAsync(query);</code></example>
    Task<Result<System.Collections.Immutable.ImmutableArray<string>>> FindFacetsAsync(AuditFacetQuery query, CancellationToken cancellationToken = default) => Task.FromResult(Result<System.Collections.Immutable.ImmutableArray<string>>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Fixes a committed upper boundary without forcing a queue flush.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.OpenExtractionAsync(plan);</code></example>
    Task<Result<AuditExtractionCursor>> OpenExtractionAsync(AuditExtractionPlan plan, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditExtractionCursor>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Reads a bounded committed prefix and revalidates lifecycle and availability.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.ReadExtractionAsync(cursor);</code></example>
    Task<Result<AuditExtractionBatch>> ReadExtractionAsync(AuditExtractionCursor cursor, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditExtractionBatch>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Archives complete finalized graphs by CompletedUtc total age.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.ArchiveBeforeAsync(cutoff);</code></example>
    Task<Result<long>> ArchiveBeforeAsync(DateTimeOffset cutoff, int batchSize = 100, CancellationToken cancellationToken = default) => Task.FromResult(Result<long>.Failure(new AuditError("AuditCapabilityUnsupported")));

    /// <summary>Removes eligible archived owned graphs and advances durable replay/availability state.</summary>
    /// <remarks>A custom provider must implement the capability or explicitly return unsupported; criteria are never ignored.</remarks>
    /// <example><code>await provider.RemoveArchivedBeforeAsync(cutoff);</code></example>
    Task<Result<long>> RemoveArchivedBeforeAsync(DateTimeOffset cutoff, int batchSize = 100, CancellationToken cancellationToken = default) => Task.FromResult(Result<long>.Failure(new AuditError("AuditCapabilityUnsupported")));
}
