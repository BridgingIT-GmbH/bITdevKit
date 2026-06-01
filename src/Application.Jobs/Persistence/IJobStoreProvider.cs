// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Aggregates the provider-neutral Jobs persistence stores.
/// </summary>
public interface IJobStoreProvider
{
    /// <summary>
    /// Gets the job runtime state store.
    /// </summary>
    IJobRuntimeStateStore RuntimeStates { get; }

    /// <summary>
    /// Gets the trigger runtime state store.
    /// </summary>
    IJobTriggerRuntimeStateStore TriggerRuntimeStates { get; }

    /// <summary>
    /// Gets the occurrence store.
    /// </summary>
    IJobOccurrenceStore Occurrences { get; }

    /// <summary>
    /// Gets the execution store.
    /// </summary>
    IJobExecutionStore Executions { get; }

    /// <summary>
    /// Gets the dependency store.
    /// </summary>
    IJobOccurrenceDependencyStore Dependencies { get; }

    /// <summary>
    /// Gets the batch store.
    /// </summary>
    IJobBatchStore Batches { get; }

    /// <summary>
    /// Gets the lease store.
    /// </summary>
    IJobLeaseStore Leases { get; }

    /// <summary>
    /// Gets the execution history store.
    /// </summary>
    IJobExecutionHistoryStore ExecutionHistory { get; }

    /// <summary>
    /// Gets the batch history store.
    /// </summary>
    IJobBatchHistoryStore BatchHistory { get; }

    /// <summary>
    /// Gets the accepted-event store.
    /// </summary>
    IJobAcceptedEventStore AcceptedEvents { get; }

    /// <summary>
    /// Gets the previous execution lookup store.
    /// </summary>
    IJobPreviousExecutionStore PreviousExecutions { get; }

    /// <summary>
    /// Gets the provider-neutral query store.
    /// </summary>
    IJobSchedulerQueryStore Queries { get; }
}
