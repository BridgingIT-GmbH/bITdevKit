// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Persists the latest durable orchestration instance snapshot.
/// </summary>
public interface IOrchestrationInstanceStore
{
    /// <summary>
    /// Creates a new orchestration instance snapshot from the current context.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="context">The orchestration context to persist.</param>
    /// <param name="concurrencyKey">The optional concurrency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted snapshot.</returns>
    Task<OrchestrationInstanceSnapshot> CreateAsync<TData>(
        OrchestrationContext<TData> context,
        string concurrencyKey = null,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData;

    /// <summary>
    /// Loads the latest snapshot for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted snapshot when found; otherwise <c>null</c>.</returns>
    Task<OrchestrationInstanceSnapshot> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists an updated orchestration instance snapshot using optimistic concurrency.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="snapshot">The previously loaded snapshot.</param>
    /// <param name="context">The updated orchestration context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted snapshot with the new version.</returns>
    Task<OrchestrationInstanceSnapshot> SaveAsync<TData>(
        OrchestrationInstanceSnapshot snapshot,
        OrchestrationContext<TData> context,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData;
}

/// <summary>
/// Coordinates exclusive orchestration instance leases.
/// </summary>
public interface IOrchestrationLeaseStore
{
    /// <summary>
    /// Acquires an exclusive lease for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="owner">The lease owner identity.</param>
    /// <param name="duration">The lease duration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acquired lease.</returns>
    Task<OrchestrationLease> AcquireAsync(
        Guid instanceId,
        string owner,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews an existing lease for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="leaseId">The lease identifier.</param>
    /// <param name="owner">The lease owner identity.</param>
    /// <param name="duration">The new lease duration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The renewed lease.</returns>
    Task<OrchestrationLease> RenewAsync(
        Guid instanceId,
        Guid leaseId,
        string owner,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases an owned lease.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="leaseId">The lease identifier.</param>
    /// <param name="owner">The lease owner identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReleaseAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that the specified owner still holds the active lease.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="leaseId">The lease identifier.</param>
    /// <param name="owner">The lease owner identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the lease is still valid and owned by the caller; otherwise <c>false</c>.</returns>
    Task<bool> VerifyAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default);
}

/// <summary>
/// Appends durable execution history records.
/// </summary>
public interface IOrchestrationHistoryStore
{
    /// <summary>
    /// Appends a durable history record.
    /// </summary>
    /// <param name="entry">The history entry to append.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted history entry.</returns>
    Task<OrchestrationHistoryEntry> AppendAsync(OrchestrationHistoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads durable history for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted history records in append order.</returns>
    Task<IReadOnlyCollection<OrchestrationHistoryEntry>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists and updates durable orchestration signals.
/// </summary>
public interface IOrchestrationSignalStore
{
    /// <summary>
    /// Persists a signal before it is processed.
    /// </summary>
    /// <typeparam name="TPayload">The signal payload type.</typeparam>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="signalName">The signal name.</param>
    /// <param name="payload">The signal payload.</param>
    /// <param name="currentState">The optional state at the time of signal acceptance.</param>
    /// <param name="idempotencyKey">The optional caller-supplied idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted signal record.</returns>
    Task<OrchestrationSignalRecord> PersistAsync<TPayload>(
        Guid instanceId,
        string signalName,
        TPayload payload,
        string currentState = null,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all persisted signals for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted signals.</returns>
    Task<IReadOnlyCollection<OrchestrationSignalRecord>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads pending signals eligible for processing in the specified state.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="currentState">The current persisted business state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The processable signal records.</returns>
    Task<IReadOnlyCollection<OrchestrationSignalRecord>> GetProcessableAsync(
        Guid instanceId,
        string currentState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the durable status of a persisted signal.
    /// </summary>
    /// <param name="signalId">The signal identifier.</param>
    /// <param name="status">The new signal status.</param>
    /// <param name="reason">The optional status reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated signal record.</returns>
    Task<OrchestrationSignalRecord> UpdateStatusAsync(
        Guid signalId,
        OrchestrationSignalStatus status,
        string reason = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists durable orchestration timers.
/// </summary>
public interface IOrchestrationTimerStore
{
    /// <summary>
    /// Persists a newly scheduled timer.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="triggerKind">The timer trigger kind.</param>
    /// <param name="dueTime">The due time in UTC.</param>
    /// <param name="targetState">The optional target state.</param>
    /// <param name="continuation">The optional continuation metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted timer record.</returns>
    Task<OrchestrationTimerRecord> ScheduleAsync(
        Guid instanceId,
        string triggerKind,
        DateTimeOffset dueTime,
        string targetState = null,
        string continuation = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads persisted timers for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted timer records.</returns>
    Task<IReadOnlyCollection<OrchestrationTimerRecord>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads due timers in deterministic order.
    /// </summary>
    /// <param name="asOfUtc">The comparison time in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The due timer records.</returns>
    Task<IReadOnlyCollection<OrchestrationTimerRecord>> GetDueAsync(
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the durable status of a persisted timer.
    /// </summary>
    /// <param name="timerId">The timer identifier.</param>
    /// <param name="status">The new timer status.</param>
    /// <param name="reason">The optional status reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated timer record.</returns>
    Task<OrchestrationTimerRecord> UpdateStatusAsync(
        Guid timerId,
        OrchestrationTimerStatus status,
        string reason = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exposes persisted orchestration state for support, operations, and administration use cases.
/// </summary>
public interface IOrchestrationQueryStore
{
    /// <summary>
    /// Loads the current orchestration instance snapshot by identifier.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted snapshot when found; otherwise <c>null</c>.</returns>
    Task<OrchestrationInstanceSnapshot> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads and deserializes the current orchestration context snapshot by identifier.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="services">The service provider used when rehydrating the context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rehydrated orchestration context when found; otherwise <c>null</c>.</returns>
    Task<OrchestrationContext<TData>> GetContextAsync<TData>(
        Guid instanceId,
        IServiceProvider services = null,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData;

    /// <summary>
    /// Queries persisted orchestration instances.
    /// </summary>
    /// <param name="query">The query criteria.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The paged query result.</returns>
    Task<OrchestrationInstanceQueryResult> QueryAsync(
        OrchestrationInstanceQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads execution history for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted history records.</returns>
    Task<IReadOnlyCollection<OrchestrationHistoryEntry>> GetHistoryAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads persisted signal records for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted signal records.</returns>
    Task<IReadOnlyCollection<OrchestrationSignalRecord>> GetSignalsAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads persisted timer records for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted timer records.</returns>
    Task<IReadOnlyCollection<OrchestrationTimerRecord>> GetTimersAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets basic metrics derived from persisted orchestration state.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted-state metrics snapshot.</returns>
    Task<OrchestrationMetricsSnapshot> GetMetricsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Performs persisted orchestration administration and repair operations.
/// </summary>
public interface IOrchestrationAdministrationStore
{
    /// <summary>
    /// Archives a persisted orchestration instance and its retained operational data.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the instance transitioned to archived state; otherwise <c>false</c> when it was already archived.</returns>
    Task<bool> ArchiveAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges persisted orchestration data matching the supplied maintenance criteria.
    /// </summary>
    /// <param name="request">The purge criteria.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The purge summary.</returns>
    Task<OrchestrationPurgeResult> PurgeAsync(OrchestrationPurgeCriteria request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases an active persisted lease for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReleaseLeaseAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues persisted timers for an orchestration instance so they can be consumed again.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of timers requeued.</returns>
    Task<int> RequeueTimersAsync(Guid instanceId, CancellationToken cancellationToken = default);
}