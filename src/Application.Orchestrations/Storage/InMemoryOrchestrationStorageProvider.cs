// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides an in-memory orchestration persistence implementation for runtime and unit-test scenarios.
/// </summary>
public class InMemoryOrchestrationStorageProvider :
    IOrchestrationStorageProvider,
    IOrchestrationInstanceStore,
    IOrchestrationLeaseStore,
    IOrchestrationHistoryStore,
    IOrchestrationSignalStore,
    IOrchestrationTimerStore,
    IOrchestrationQueryStore,
    IOrchestrationAdministrationStore
{
    private readonly object sync = new();
    private readonly ICurrentUserAccessor currentUserAccessor;
    private readonly IOrchestrationClock clock;
    private readonly Dictionary<Guid, OrchestrationInstanceSnapshot> instances = [];
    private readonly List<OrchestrationHistoryEntry> history = [];
    private readonly Dictionary<Guid, OrchestrationSignalRecord> signals = [];
    private readonly Dictionary<Guid, OrchestrationTimerRecord> timers = [];
    private readonly Dictionary<Guid, OrchestrationLease> leases = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryOrchestrationStorageProvider"/> class.
    /// </summary>
    /// <param name="serializer">The serializer used for durable payloads.</param>
    /// <param name="currentUserAccessor">The current user accessor used for audit metadata.</param>
    /// <param name="clock">The orchestration clock.</param>
    public InMemoryOrchestrationStorageProvider(
        ISerializer serializer = null,
        ICurrentUserAccessor currentUserAccessor = null,
        IOrchestrationClock clock = null)
    {
        this.Serializer = serializer ?? new SystemTextJsonSerializer();
        this.currentUserAccessor = currentUserAccessor;
        this.clock = clock ?? new SystemOrchestrationClock();
    }

    /// <inheritdoc />
    public IOrchestrationInstanceStore Instances => this;

    /// <inheritdoc />
    public IOrchestrationLeaseStore Leases => this;

    /// <inheritdoc />
    public IOrchestrationHistoryStore History => this;

    /// <inheritdoc />
    public IOrchestrationSignalStore Signals => this;

    /// <inheritdoc />
    public IOrchestrationTimerStore Timers => this;

    /// <inheritdoc />
    public IOrchestrationQueryStore Queries => this;

    /// <inheritdoc />
    public IOrchestrationAdministrationStore Administration => this;

    /// <inheritdoc />
    public ISerializer Serializer { get; }

    /// <inheritdoc />
    public Task<OrchestrationInstanceSnapshot> CreateAsync<TData>(
        OrchestrationContext<TData> context,
        string concurrencyKey = null,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (this.instances.ContainsKey(context.InstanceId))
            {
                throw new InvalidOperationException($"Orchestration instance '{context.InstanceId}' already exists.");
            }

            var now = this.clock.UtcNow;
            var actor = this.GetCurrentActor();
            var snapshot = this.BuildSnapshot(context, 1, concurrencyKey, now, now, actor, actor);
            this.instances[context.InstanceId] = snapshot;

            return Task.FromResult(this.Clone(snapshot));
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationInstanceSnapshot> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            this.instances.TryGetValue(instanceId, out var snapshot);
            return Task.FromResult(this.Clone(snapshot));
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationInstanceSnapshot> SaveAsync<TData>(
        OrchestrationInstanceSnapshot snapshot,
        OrchestrationContext<TData> context,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.instances.TryGetValue(snapshot.InstanceId, out var existing))
            {
                throw new KeyNotFoundException($"Orchestration instance '{snapshot.InstanceId}' was not found.");
            }

            if (snapshot.Version != existing.Version)
            {
                throw new InvalidOperationException(
                    $"Orchestration instance '{snapshot.InstanceId}' version mismatch. Expected '{existing.Version}', received '{snapshot.Version}'.");
            }

            var now = this.clock.UtcNow;
            var actor = this.GetCurrentActor();
            var updated = this.BuildSnapshot(
                context,
                existing.Version + 1,
                snapshot.ConcurrencyKey,
                existing.CreatedDate,
                now,
                existing.CreatedBy,
                actor,
                existing.IsArchived,
                existing.ArchivedUtc);
            this.instances[snapshot.InstanceId] = updated;

            return Task.FromResult(this.Clone(updated));
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationLease> AcquireAsync(
        Guid instanceId,
        string owner,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var now = this.clock.UtcNow;
            if (this.leases.TryGetValue(instanceId, out var existing) && existing.ExpiresUtc > now)
            {
                throw new InvalidOperationException($"Orchestration instance '{instanceId}' is already leased to '{existing.Owner}'.");
            }

            var lease = new OrchestrationLease
            {
                LeaseId = Guid.NewGuid(),
                InstanceId = instanceId,
                Owner = owner,
                AcquiredUtc = now,
                ExpiresUtc = now.Add(duration),
            };

            this.leases[instanceId] = lease;
            return Task.FromResult(this.Clone(lease));
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationLease> RenewAsync(
        Guid instanceId,
        Guid leaseId,
        string owner,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.leases.TryGetValue(instanceId, out var existing) ||
                existing.LeaseId != leaseId ||
                !string.Equals(existing.Owner, owner, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Lease '{leaseId}' for orchestration instance '{instanceId}' is not owned by '{owner}'.");
            }

            if (existing.ExpiresUtc <= this.clock.UtcNow)
            {
                throw new InvalidOperationException($"Lease '{leaseId}' for orchestration instance '{instanceId}' has already expired.");
            }

            var renewed = existing with { ExpiresUtc = this.clock.UtcNow.Add(duration) };
            this.leases[instanceId] = renewed;

            return Task.FromResult(this.Clone(renewed));
        }
    }

    /// <inheritdoc />
    public Task ReleaseAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.leases.TryGetValue(instanceId, out var existing) ||
                existing.LeaseId != leaseId ||
                !string.Equals(existing.Owner, owner, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Lease '{leaseId}' for orchestration instance '{instanceId}' is not owned by '{owner}'.");
            }

            this.leases.Remove(instanceId);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task<bool> VerifyAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var isValid = this.leases.TryGetValue(instanceId, out var existing) &&
                existing.LeaseId == leaseId &&
                string.Equals(existing.Owner, owner, StringComparison.Ordinal) &&
                existing.ExpiresUtc > this.clock.UtcNow;

            return Task.FromResult(isValid);
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationHistoryEntry> AppendAsync(OrchestrationHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var recordedAt = entry.RecordedAt == default ? this.clock.UtcNow : entry.RecordedAt;
            var latestRecordedAt = this.history
                .Where(item => item.InstanceId == entry.InstanceId)
                .OrderByDescending(item => item.RecordedAt)
                .Select(item => item.RecordedAt)
                .FirstOrDefault();

            if (latestRecordedAt != default && recordedAt <= latestRecordedAt)
            {
                recordedAt = latestRecordedAt.AddTicks(1);
            }

            var persisted = entry with
            {
                EntryId = entry.EntryId == Guid.Empty ? Guid.NewGuid() : entry.EntryId,
                RecordedAt = recordedAt,
                RecordedBy = string.IsNullOrWhiteSpace(entry.RecordedBy) ? this.GetCurrentActor() : entry.RecordedBy,
            };

            this.history.Add(persisted);
            return Task.FromResult(this.Clone(persisted));
        }
    }

    /// <inheritdoc />
    Task<IReadOnlyCollection<OrchestrationHistoryEntry>> IOrchestrationHistoryStore.GetAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var items = this.history
                .Where(item => item.InstanceId == instanceId)
                .OrderBy(item => item.RecordedAt)
                .ThenBy(item => item.EntryId)
                .Select(this.Clone)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<OrchestrationHistoryEntry>>(items);
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationSignalRecord> PersistAsync<TPayload>(
        Guid instanceId,
        string signalName,
        TPayload payload,
        string currentState = null,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var duplicate = this.signals.Values.FirstOrDefault(item =>
                    item.InstanceId == instanceId &&
                    string.Equals(item.SignalName, signalName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.IdempotencyKey, idempotencyKey, StringComparison.Ordinal));

                if (duplicate is not null)
                {
                    return Task.FromResult(this.Clone(duplicate));
                }
            }

            var now = this.clock.UtcNow;
            var actor = this.GetCurrentActor();
            var persisted = new OrchestrationSignalRecord
            {
                SignalId = Guid.NewGuid(),
                InstanceId = instanceId,
                SignalName = signalName,
                CurrentState = currentState,
                Payload = this.Serializer.SerializeToString(payload),
                PayloadType = payload?.GetType().AssemblyQualifiedName ?? typeof(TPayload).AssemblyQualifiedName,
                IdempotencyKey = idempotencyKey,
                Status = OrchestrationSignalStatus.Pending,
                ReceivedUtc = now,
                CreatedDate = now,
                UpdatedDate = now,
                CreatedBy = actor,
                UpdatedBy = actor,
            };

            this.signals[persisted.SignalId] = persisted;
            return Task.FromResult(this.Clone(persisted));
        }
    }

    /// <inheritdoc />
    Task<IReadOnlyCollection<OrchestrationSignalRecord>> IOrchestrationSignalStore.GetAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var items = this.signals.Values
                .Where(item => item.InstanceId == instanceId)
                .OrderBy(item => item.ReceivedUtc)
                .ThenBy(item => item.SignalId)
                .Select(this.Clone)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<OrchestrationSignalRecord>>(items);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<OrchestrationSignalRecord>> GetProcessableAsync(
        Guid instanceId,
        string currentState,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var items = this.signals.Values
                .Where(item => item.InstanceId == instanceId)
                .Where(item => item.Status == OrchestrationSignalStatus.Pending)
                .Where(item => string.IsNullOrWhiteSpace(item.CurrentState) || string.Equals(item.CurrentState, currentState, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.ReceivedUtc)
                .ThenBy(item => item.SignalId)
                .Select(this.Clone)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<OrchestrationSignalRecord>>(items);
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationSignalRecord> UpdateStatusAsync(
        Guid signalId,
        OrchestrationSignalStatus status,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.signals.TryGetValue(signalId, out var existing))
            {
                throw new KeyNotFoundException($"Signal '{signalId}' was not found.");
            }

            var now = this.clock.UtcNow;
            var actor = this.GetCurrentActor();
            var updated = existing with
            {
                Status = status,
                StatusReason = reason,
                ProcessedUtc = status == OrchestrationSignalStatus.Pending ? existing.ProcessedUtc : now,
                UpdatedDate = now,
                UpdatedBy = actor,
            };

            this.signals[signalId] = updated;
            return Task.FromResult(this.Clone(updated));
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationTimerRecord> ScheduleAsync(
        Guid instanceId,
        string triggerKind,
        DateTimeOffset dueTime,
        string targetState = null,
        string continuation = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerKind);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var now = this.clock.UtcNow;
            var actor = this.GetCurrentActor();
            var persisted = new OrchestrationTimerRecord
            {
                TimerId = Guid.NewGuid(),
                InstanceId = instanceId,
                TriggerKind = triggerKind,
                DueTimeUtc = dueTime,
                TargetState = targetState,
                Continuation = continuation,
                Status = OrchestrationTimerStatus.Pending,
                CreatedDate = now,
                UpdatedDate = now,
                CreatedBy = actor,
                UpdatedBy = actor,
            };

            this.timers[persisted.TimerId] = persisted;
            return Task.FromResult(this.Clone(persisted));
        }
    }

    /// <inheritdoc />
    Task<IReadOnlyCollection<OrchestrationTimerRecord>> IOrchestrationTimerStore.GetAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var items = this.timers.Values
                .Where(item => item.InstanceId == instanceId)
                .OrderBy(item => item.DueTimeUtc)
                .ThenBy(item => item.CreatedDate)
                .ThenBy(item => item.TimerId)
                .Select(this.Clone)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<OrchestrationTimerRecord>>(items);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<OrchestrationTimerRecord>> GetDueAsync(
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var items = this.timers.Values
                .Where(item => item.Status == OrchestrationTimerStatus.Pending)
                .Where(item => item.DueTimeUtc <= asOfUtc)
                .OrderBy(item => item.DueTimeUtc)
                .ThenBy(item => item.CreatedDate)
                .ThenBy(item => item.TimerId)
                .Select(this.Clone)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<OrchestrationTimerRecord>>(items);
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationTimerRecord> UpdateStatusAsync(
        Guid timerId,
        OrchestrationTimerStatus status,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.timers.TryGetValue(timerId, out var existing))
            {
                throw new KeyNotFoundException($"Timer '{timerId}' was not found.");
            }

            var now = this.clock.UtcNow;
            var actor = this.GetCurrentActor();
            var updated = existing with
            {
                Status = status,
                StatusReason = reason,
                ProcessedUtc = status == OrchestrationTimerStatus.Pending ? existing.ProcessedUtc : now,
                UpdatedDate = now,
                UpdatedBy = actor,
            };

            this.timers[timerId] = updated;
            return Task.FromResult(this.Clone(updated));
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationInstanceSnapshot> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return this.GetAsync(instanceId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrchestrationContext<TData>> GetContextAsync<TData>(
        Guid instanceId,
        IServiceProvider services = null,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        var snapshot = await this.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        return this.RestoreContext<TData>(snapshot, services ?? NullServiceProvider.Instance);
    }

    /// <inheritdoc />
    public Task<OrchestrationInstanceQueryResult> QueryAsync(
        OrchestrationInstanceQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        query ??= new OrchestrationInstanceQuery();

        lock (this.sync)
        {
            var items = this.instances.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.OrchestrationName))
            {
                items = items.Where(item => string.Equals(item.OrchestrationName, query.OrchestrationName, StringComparison.OrdinalIgnoreCase));
            }

            if (query.Statuses is { Count: > 0 })
            {
                var statusSet = new HashSet<OrchestrationStatus>(query.Statuses);
                items = items.Where(item => statusSet.Contains(item.Status));
            }

            if (query.States is { Count: > 0 })
            {
                var stateSet = new HashSet<string>(query.States, StringComparer.OrdinalIgnoreCase);
                items = items.Where(item => stateSet.Contains(item.CurrentState ?? string.Empty));
            }

            if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            {
                items = items.Where(item => string.Equals(item.CorrelationId, query.CorrelationId, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.ConcurrencyKey))
            {
                items = items.Where(item => string.Equals(item.ConcurrencyKey, query.ConcurrencyKey, StringComparison.OrdinalIgnoreCase));
            }

            if (query.StartedFromUtc.HasValue)
            {
                items = items.Where(item => item.StartedUtc >= query.StartedFromUtc.Value);
            }

            if (query.StartedToUtc.HasValue)
            {
                items = items.Where(item => item.StartedUtc <= query.StartedToUtc.Value);
            }

            if (query.CompletedFromUtc.HasValue)
            {
                items = items.Where(item => item.CompletedUtc.HasValue && item.CompletedUtc.Value >= query.CompletedFromUtc.Value);
            }

            if (query.CompletedToUtc.HasValue)
            {
                items = items.Where(item => item.CompletedUtc.HasValue && item.CompletedUtc.Value <= query.CompletedToUtc.Value);
            }

            items = items
                .OrderByDescending(item => item.StartedUtc)
                .ThenBy(item => item.InstanceId);

            var total = items.Count();
            var paged = items
                .Skip(Math.Max(0, query.Skip))
                .Take(query.Take <= 0 ? 100 : query.Take)
                .Select(this.Clone)
                .ToArray();

            return Task.FromResult(new OrchestrationInstanceQueryResult
            {
                TotalCount = total,
                Items = paged,
            });
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<OrchestrationHistoryEntry>> GetHistoryAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return ((IOrchestrationHistoryStore)this).GetAsync(instanceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<OrchestrationSignalRecord>> GetSignalsAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return ((IOrchestrationSignalStore)this).GetAsync(instanceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<OrchestrationTimerRecord>> GetTimersAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return ((IOrchestrationTimerStore)this).GetAsync(instanceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OrchestrationMetricsSnapshot> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var snapshots = this.instances.Values.ToArray();
            var countsByOrchestration = snapshots
                .GroupBy(item => item.OrchestrationName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            var metrics = new OrchestrationMetricsSnapshot
            {
                TotalInstances = snapshots.Length,
                RunningInstances = snapshots.Count(item => item.Status == OrchestrationStatus.Running),
                WaitingInstances = snapshots.Count(item => item.Status == OrchestrationStatus.Waiting),
                PausedInstances = snapshots.Count(item => item.Status == OrchestrationStatus.Paused),
                CompletedInstances = snapshots.Count(item => item.Status == OrchestrationStatus.Completed),
                CancelledInstances = snapshots.Count(item => item.Status == OrchestrationStatus.Cancelled),
                FailedInstances = snapshots.Count(item => item.Status == OrchestrationStatus.Failed),
                TerminatedInstances = snapshots.Count(item => item.Status == OrchestrationStatus.Terminated),
                OldestWaitingStartedUtc = snapshots
                    .Where(item => item.Status == OrchestrationStatus.Waiting)
                    .OrderBy(item => item.StartedUtc)
                    .Select(item => (DateTimeOffset?)item.StartedUtc)
                    .FirstOrDefault(),
                InstanceCountsByOrchestration = countsByOrchestration,
                HistoryCount = this.history.Count,
                SignalCount = this.signals.Count,
                TimerCount = this.timers.Count,
            };

            return Task.FromResult(metrics);
        }
    }

    /// <inheritdoc />
    public Task<bool> ArchiveAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.instances.TryGetValue(instanceId, out var snapshot))
            {
                throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
            }

            if (snapshot.IsArchived)
            {
                return Task.FromResult(false);
            }

            var now = this.clock.UtcNow;
            var actor = this.GetCurrentActor();
            this.instances[instanceId] = snapshot with
            {
                IsArchived = true,
                ArchivedUtc = now,
                Version = snapshot.Version + 1,
                UpdatedDate = now,
                UpdatedBy = actor,
            };

            this.history.Add(new OrchestrationHistoryEntry
            {
                EntryId = Guid.NewGuid(),
                InstanceId = instanceId,
                EventType = "Archived",
                StateName = snapshot.CurrentState,
                ActivityName = snapshot.CurrentActivity,
                Details = "Administrative archive",
                RecordedAt = now,
                RecordedBy = actor,
            });

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<OrchestrationPurgeResult> PurgeAsync(OrchestrationPurgeCriteria request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request ??= new OrchestrationPurgeCriteria();

        lock (this.sync)
        {
            var candidateIds = this.instances.Values
                .Where(item => !request.IsArchived.HasValue || item.IsArchived == request.IsArchived.Value)
                .Where(item => request.Statuses.Count == 0 || request.Statuses.Contains(item.Status))
                .Where(item => !request.OlderThan.HasValue || GetRetentionTimestamp(item) <= request.OlderThan.Value)
                .Select(item => item.InstanceId)
                .ToHashSet();

            var historyCount = this.history.RemoveAll(item => candidateIds.Contains(item.InstanceId));
            var signalIds = this.signals.Values.Where(item => candidateIds.Contains(item.InstanceId)).Select(item => item.SignalId).ToArray();
            foreach (var signalId in signalIds)
            {
                this.signals.Remove(signalId);
            }

            var timerIds = this.timers.Values.Where(item => candidateIds.Contains(item.InstanceId)).Select(item => item.TimerId).ToArray();
            foreach (var timerId in timerIds)
            {
                this.timers.Remove(timerId);
            }

            foreach (var instanceId in candidateIds)
            {
                this.instances.Remove(instanceId);
                this.leases.Remove(instanceId);
            }

            return Task.FromResult(new OrchestrationPurgeResult
            {
                PurgedInstanceCount = candidateIds.Count,
                PurgedHistoryCount = historyCount,
                PurgedSignalCount = signalIds.Length,
                PurgedTimerCount = timerIds.Length,
            });
        }
    }

    /// <inheritdoc />
    public Task ReleaseLeaseAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.instances.TryGetValue(instanceId, out var snapshot))
            {
                throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
            }

            if (!this.leases.Remove(instanceId))
            {
                throw new InvalidOperationException($"Orchestration instance '{instanceId}' does not have an active lease.");
            }

            var now = this.clock.UtcNow;
            var actor = this.GetCurrentActor();
            this.instances[instanceId] = snapshot with
            {
                Version = snapshot.Version + 1,
                UpdatedDate = now,
                UpdatedBy = actor,
            };

            this.history.Add(new OrchestrationHistoryEntry
            {
                EntryId = Guid.NewGuid(),
                InstanceId = instanceId,
                EventType = "LeaseReleased",
                StateName = snapshot.CurrentState,
                ActivityName = snapshot.CurrentActivity,
                Details = "Administrative lease release",
                RecordedAt = now,
                RecordedBy = actor,
            });

            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task<int> RequeueTimersAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.instances.TryGetValue(instanceId, out var snapshot))
            {
                throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
            }

            var rows = this.timers.Values
                .Where(item => item.InstanceId == instanceId)
                .Where(item => item.Status != OrchestrationTimerStatus.Pending)
                .OrderBy(item => item.DueTimeUtc)
                .ThenBy(item => item.TimerId)
                .ToArray();

            if (rows.Length == 0)
            {
                throw new InvalidOperationException($"Orchestration instance '{instanceId}' does not have requeueable timers.");
            }

            var now = this.clock.UtcNow;
            var actor = this.GetCurrentActor();
            foreach (var row in rows)
            {
                this.timers[row.TimerId] = row with
                {
                    Status = OrchestrationTimerStatus.Pending,
                    DueTimeUtc = now,
                    ProcessedUtc = null,
                    StatusReason = "Requeued by administration.",
                    UpdatedDate = now,
                    UpdatedBy = actor,
                };
            }

            this.instances[instanceId] = snapshot with
            {
                Version = snapshot.Version + 1,
                UpdatedDate = now,
                UpdatedBy = actor,
            };

            this.history.Add(new OrchestrationHistoryEntry
            {
                EntryId = Guid.NewGuid(),
                InstanceId = instanceId,
                EventType = "TimersRequeued",
                StateName = snapshot.CurrentState,
                ActivityName = snapshot.CurrentActivity,
                Details = rows.Length.ToString(),
                RecordedAt = now,
                RecordedBy = actor,
            });

            return Task.FromResult(rows.Length);
        }
    }

    private OrchestrationInstanceSnapshot BuildSnapshot<TData>(
        OrchestrationContext<TData> context,
        int version,
        string concurrencyKey,
        DateTimeOffset createdDate,
        DateTimeOffset updatedDate,
        string createdBy,
        string updatedBy,
        bool isArchived = false,
        DateTimeOffset? archivedUtc = null)
        where TData : class, IOrchestrationData
    {
        return new OrchestrationInstanceSnapshot
        {
            InstanceId = context.InstanceId,
            OrchestrationName = context.OrchestrationName,
            Status = context.Status,
            CurrentState = context.CurrentState,
            CurrentActivity = context.CurrentActivity,
            CorrelationId = context.CorrelationId,
            ConcurrencyKey = concurrencyKey,
            StartedUtc = context.StartedUtc,
            CompletedUtc = context.CompletedUtc,
            ContextType = typeof(TData).AssemblyQualifiedName,
            SerializedContext = this.Serializer.SerializeToString(this.CaptureContext(context)),
            Version = version,
            IsArchived = isArchived,
            ArchivedUtc = archivedUtc,
            CreatedDate = createdDate,
            UpdatedDate = updatedDate,
            CreatedBy = createdBy,
            UpdatedBy = updatedBy,
        };
    }

    private OrchestrationContextSnapshot<TData> CaptureContext<TData>(OrchestrationContext<TData> context)
        where TData : class, IOrchestrationData
    {
        return new OrchestrationContextSnapshot<TData>
        {
            OrchestrationName = context.OrchestrationName,
            InstanceId = context.InstanceId,
            CorrelationId = context.CorrelationId,
            Data = context.Data,
            Properties = context.Properties.ToDictionary(
                item => item.Key,
                item => this.CaptureProperty(item.Value),
                StringComparer.OrdinalIgnoreCase),
            Status = context.Status,
            CurrentState = context.CurrentState,
            CurrentActivity = context.CurrentActivity,
            StartedUtc = context.StartedUtc,
            CompletedUtc = context.CompletedUtc,
            LastOutcome = context.LastOutcome,
            FailureReason = context.FailureReason,
        };
    }

    private OrchestrationContextPropertySnapshot CaptureProperty(object value)
    {
        return new OrchestrationContextPropertySnapshot
        {
            TypeName = value?.GetType().AssemblyQualifiedName,
            SerializedValue = value is null ? null : this.Serializer.SerializeToString(value),
        };
    }

    private OrchestrationContext<TData> RestoreContext<TData>(OrchestrationInstanceSnapshot snapshot, IServiceProvider services)
        where TData : class, IOrchestrationData
    {
        var state = this.Serializer.Deserialize<OrchestrationContextSnapshot<TData>>(snapshot.SerializedContext);
        var context = new OrchestrationContext<TData>(
            state.OrchestrationName,
            state.Data,
            services,
            state.InstanceId,
            state.CorrelationId,
            state.StartedUtc)
        {
            Status = state.Status,
            CurrentState = state.CurrentState,
            CurrentActivity = state.CurrentActivity,
            CompletedUtc = state.CompletedUtc,
            LastOutcome = state.LastOutcome,
            FailureReason = state.FailureReason,
        };

        foreach (var property in state.Properties)
        {
            context.Properties[property.Key] = this.RestoreProperty(property.Value);
        }

        return context;
    }

    private object RestoreProperty(OrchestrationContextPropertySnapshot property)
    {
        if (property is null || string.IsNullOrWhiteSpace(property.TypeName))
        {
            return null;
        }

        var type = Type.GetType(property.TypeName, throwOnError: false);
        return type is null ? property.SerializedValue : this.Serializer.Deserialize(property.SerializedValue, type);
    }

    private string GetCurrentActor()
    {
        if (this.currentUserAccessor is null || !this.currentUserAccessor.IsAuthenticated)
        {
            return null;
        }

        return this.currentUserAccessor.UserId ?? this.currentUserAccessor.UserName ?? this.currentUserAccessor.Email;
    }

    private static DateTimeOffset GetRetentionTimestamp(OrchestrationInstanceSnapshot snapshot)
    {
        return snapshot.ArchivedUtc ?? snapshot.CompletedUtc ?? snapshot.UpdatedDate;
    }

    private OrchestrationInstanceSnapshot Clone(OrchestrationInstanceSnapshot snapshot)
    {
        return snapshot is null ? null : snapshot with { };
    }

    private OrchestrationHistoryEntry Clone(OrchestrationHistoryEntry entry)
    {
        return entry is null ? null : entry with { };
    }

    private OrchestrationSignalRecord Clone(OrchestrationSignalRecord signal)
    {
        return signal is null ? null : signal with { };
    }

    private OrchestrationTimerRecord Clone(OrchestrationTimerRecord timer)
    {
        return timer is null ? null : timer with { };
    }

    private OrchestrationLease Clone(OrchestrationLease lease)
    {
        return lease is null ? null : lease with { };
    }

    private class NullServiceProvider : IServiceProvider
    {
        public static NullServiceProvider Instance { get; } = new();

        public object GetService(Type serviceType)
        {
            return null;
        }
    }
}
