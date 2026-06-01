// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Defines the Entity Framework capability contract required by the durable jobs provider.
/// </summary>
/// <remarks>
/// A host <see cref="DbContext" /> opts into jobs persistence by implementing this interface and
/// exposing the jobs sets alongside any other feature-specific sets it already supports. The jobs
/// entities carry their Entity Framework mapping through attributes and conventions, so no additional
/// jobs-specific model builder call is required.
/// </remarks>
/// <example>
/// <code>
/// public class AppDbContext : DbContext, IJobsContext
/// {
///     public DbSet&lt;JobRuntimeStateEntity&gt; JobRuntimeStates { get; set; }
///
///     public DbSet&lt;JobTriggerRuntimeStateEntity&gt; JobTriggerRuntimeStates { get; set; }
///
///     public DbSet&lt;JobOccurrenceEntity&gt; JobOccurrences { get; set; }
///
///     public DbSet&lt;JobExecutionEntity&gt; JobExecutions { get; set; }
///
///     public DbSet&lt;JobExecutionHistoryEntity&gt; JobExecutionHistory { get; set; }
///
///     public DbSet&lt;JobBatchHistoryEntity&gt; JobBatchHistory { get; set; }
///
///     public DbSet&lt;JobAcceptedEventEntity&gt; JobAcceptedEvents { get; set; }
///
/// }
/// </code>
/// </example>
public interface IJobsContext
{
    /// <summary>
    /// Gets or sets job-level runtime state rows.
    /// </summary>
    DbSet<JobRuntimeStateEntity> JobRuntimeStates { get; set; }

    /// <summary>
    /// Gets or sets trigger-level runtime state rows.
    /// </summary>
    DbSet<JobTriggerRuntimeStateEntity> JobTriggerRuntimeStates { get; set; }

    /// <summary>
    /// Gets or sets durable job occurrences.
    /// </summary>
    DbSet<JobOccurrenceEntity> JobOccurrences { get; set; }

    /// <summary>
    /// Gets or sets occurrence dependency rows used for chaining and prerequisites.
    /// </summary>
    DbSet<JobOccurrenceDependencyEntity> JobOccurrenceDependencies { get; set; }

    /// <summary>
    /// Gets or sets batch aggregate rows.
    /// </summary>
    DbSet<JobBatchEntity> JobBatches { get; set; }

    /// <summary>
    /// Gets or sets batch membership rows.
    /// </summary>
    DbSet<JobBatchOccurrenceEntity> JobBatchOccurrences { get; set; }

    /// <summary>
    /// Gets or sets execution-attempt rows.
    /// </summary>
    DbSet<JobExecutionEntity> JobExecutions { get; set; }

    /// <summary>
    /// Gets or sets append-only execution history rows.
    /// </summary>
    DbSet<JobExecutionHistoryEntity> JobExecutionHistory { get; set; }

    /// <summary>
    /// Gets or sets append-only batch history rows.
    /// </summary>
    DbSet<JobBatchHistoryEntity> JobBatchHistory { get; set; }

    /// <summary>
    /// Gets or sets accepted external events awaiting materialization.
    /// </summary>
    DbSet<JobAcceptedEventEntity> JobAcceptedEvents { get; set; }

    /// <summary>
    /// Gets or sets lease ownership rows.
    /// </summary>
    DbSet<JobLeaseEntity> JobLeases { get; set; }
}
