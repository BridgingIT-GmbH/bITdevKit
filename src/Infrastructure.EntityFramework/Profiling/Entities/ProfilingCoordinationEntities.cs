// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>Coordinates durable publication, deletion and Runtime maintenance within the provider scope.</summary>
/// <example><code>var entity = new ProfilingStoreStateEntity();</code></example>
public sealed class ProfilingStoreStateEntity
{
    /// <summary>Gets or sets the singleton coordination row identity.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    public int Id { get; set; }

    /// <summary>Gets or sets the durable store incarnation.</summary>
    /// <example><code>var value = entity.StoreEpoch;</code></example>
    public Guid StoreEpoch { get; set; }

    /// <summary>Gets or sets the opaque cursor authentication secret.</summary>
    /// <example><code>var value = entity.QuerySecret;</code></example>
    public byte[] QuerySecret { get; set; }

    /// <summary>Gets or sets the monotonically advancing writer registration generation.</summary>
    /// <example><code>var value = entity.RegistrationGeneration;</code></example>
    public long RegistrationGeneration { get; set; }

    /// <summary>Gets or sets the last committed operation publication position.</summary>
    /// <example><code>var value = entity.PublicationWatermark;</code></example>
    public long PublicationWatermark { get; set; }

    /// <summary>Gets or sets the cursor-invalidating root deletion revision.</summary>
    /// <example><code>var value = entity.DeletionRevision;</code></example>
    public long DeletionRevision { get; set; }

    /// <summary>Gets or sets the transactional retained root count.</summary>
    /// <example><code>var value = entity.RetainedOperationCount;</code></example>
    public long RetainedOperationCount { get; set; }

    /// <summary>Gets or sets the transactional charged root payload.</summary>
    /// <example><code>var value = entity.RetainedOperationBytes;</code></example>
    public long RetainedOperationBytes { get; set; }

    /// <summary>Gets or sets the transactional row-lock mutation token.</summary>
    /// <example><code>var value = entity.ConcurrencyVersion;</code></example>
    public Guid ConcurrencyVersion { get; set; }
}

/// <summary>Retains a bounded live writer lease independently of immutable history.</summary>
/// <example><code>var entity = new ProfilingWriterEntity();</code></example>
public sealed class ProfilingWriterEntity
{
    /// <summary>Gets or sets the original writer identity.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the unrepeatable original lease token.</summary>
    /// <example><code>var value = entity.Token;</code></example>
    public Guid Token { get; set; }

    /// <summary>Gets or sets the issuing store incarnation.</summary>
    /// <example><code>var value = entity.StoreEpoch;</code></example>
    public Guid StoreEpoch { get; set; }

    /// <summary>Gets or sets the stable idempotent open attempt.</summary>
    /// <example><code>var value = entity.AttemptId;</code></example>
    public Guid AttemptId { get; set; }

    /// <summary>Gets or sets the live cached process identity.</summary>
    /// <example><code>var value = entity.NodeId;</code></example>
    public Guid NodeId { get; set; }

    /// <summary>Gets or sets the shared live process descriptor.</summary>
    /// <example><code>var value = entity.Node;</code></example>
    public ProfilingNodeEntity Node { get; set; }

    /// <summary>Gets or sets the original writer registration generation.</summary>
    /// <example><code>var value = entity.Generation;</code></example>
    public long Generation { get; set; }

    /// <summary>Gets or sets the provider-authoritative lease expiry UTC.</summary>
    /// <example><code>var value = entity.ExpiresUtcTicks;</code></example>
    public long ExpiresUtcTicks { get; set; }

    /// <summary>Gets or sets confirmed non-replayable completion progress.</summary>
    /// <example><code>var value = entity.SettledThrough;</code></example>
    public long SettledThrough { get; set; }

    /// <summary>Gets or sets permanent retirement of the original identity.</summary>
    /// <example><code>var value = entity.Retired;</code></example>
    public bool Retired { get; set; }
}

/// <summary>Retains bounded recoverable clear preparation and durable replay fences.</summary>
/// <example><code>var entity = new ProfilingClearEntity();</code></example>
public sealed class ProfilingClearEntity
{
    /// <summary>Gets or sets the bounded administrative clear occurrence.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the independent history dataset selection.</summary>
    /// <example><code>var value = entity.DataSet;</code></example>
    public ProfilingDataSet DataSet { get; set; }

    /// <summary>Gets or sets the inclusive selected completion-UTC lower bound.</summary>
    /// <example><code>var value = entity.FromUtcTicks;</code></example>
    public long? FromUtcTicks { get; set; }

    /// <summary>Gets or sets the exclusive selected completion-UTC upper bound.</summary>
    /// <example><code>var value = entity.ToUtcTicks;</code></example>
    public long? ToUtcTicks { get; set; }

    /// <summary>Gets or sets provider-authoritative preparation UTC.</summary>
    /// <example><code>var value = entity.PreparedUtcTicks;</code></example>
    public long PreparedUtcTicks { get; set; }

    /// <summary>Gets or sets the unsealed preparation expiry UTC.</summary>
    /// <example><code>var value = entity.DeadlineUtcTicks;</code></example>
    public long DeadlineUtcTicks { get; set; }

    /// <summary>Gets or sets the durable fence publication UTC.</summary>
    /// <example><code>var value = entity.SealedUtcTicks;</code></example>
    public long? SealedUtcTicks { get; set; }

    /// <summary>Gets or sets the known final deletion UTC.</summary>
    /// <example><code>var value = entity.CompletedUtcTicks;</code></example>
    public long? CompletedUtcTicks { get; set; }

    /// <summary>Gets or sets the last eligible original registration generation.</summary>
    /// <example><code>var value = entity.GenerationBoundary;</code></example>
    public long GenerationBoundary { get; set; }

    /// <summary>Gets or sets the recoverable maintenance lifecycle.</summary>
    /// <example><code>var value = entity.State;</code></example>
    public ProfilingClearState State { get; set; }

    /// <summary>Gets or sets the bounded original immutable writer eligibility snapshot.</summary>
    /// <example><code>var value = entity.EligibleWritersJson;</code></example>
    public string EligibleWritersJson { get; set; }

    /// <summary>Gets or sets the bounded fixed writer cutoffs retained after lease expiry.</summary>
    /// <example><code>var value = entity.AcknowledgementsJson;</code></example>
    public string AcknowledgementsJson { get; set; }

    /// <summary>Gets or sets known deleted whole operation roots.</summary>
    /// <example><code>var value = entity.RemovedOperationCount;</code></example>
    public long RemovedOperationCount { get; set; }

    /// <summary>Gets or sets known deleted owned aggregate paths.</summary>
    /// <example><code>var value = entity.RemovedSummaryCount;</code></example>
    public long RemovedSummaryCount { get; set; }

    /// <summary>Gets or sets known deleted terminal Runtime roots.</summary>
    /// <example><code>var value = entity.RemovedRuntimeSessionCount;</code></example>
    public int RemovedRuntimeSessionCount { get; set; }

    /// <summary>Gets or sets known deleted owned Runtime observations.</summary>
    /// <example><code>var value = entity.RemovedSnapshotCount;</code></example>
    public long RemovedSnapshotCount { get; set; }
}

/// <summary>Coordinates Runtime maintenance separately from high-volume operation publication.</summary>
/// <example><code>var entity = new ProfilingRuntimeGateEntity();</code></example>
public sealed class ProfilingRuntimeGateEntity
{
    /// <summary>Gets or sets the singleton Runtime coordination row.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    public int Id { get; set; }

    /// <summary>Gets or sets the reserved Runtime start/import maintenance occurrence.</summary>
    /// <example><code>var value = entity.MaintenanceClearId;</code></example>
    public Guid? MaintenanceClearId { get; set; }

    /// <summary>Gets or sets the independent Runtime deletion revision.</summary>
    /// <example><code>var value = entity.DeletionRevision;</code></example>
    public long DeletionRevision { get; set; }

    /// <summary>Gets or sets the transactional Runtime lock mutation token.</summary>
    /// <example><code>var value = entity.ConcurrencyVersion;</code></example>
    public Guid ConcurrencyVersion { get; set; }
}
