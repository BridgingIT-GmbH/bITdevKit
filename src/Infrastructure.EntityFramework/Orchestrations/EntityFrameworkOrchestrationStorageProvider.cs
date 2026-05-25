// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Provides an Entity Framework backed orchestration persistence implementation.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IOrchestrationContext"/>.</typeparam>
public class EntityFrameworkOrchestrationStorageProvider<TContext> :
    IOrchestrationStorageProvider,
    IOrchestrationInstanceStore,
    IOrchestrationLeaseStore,
    IOrchestrationHistoryStore,
    IOrchestrationSignalStore,
    IOrchestrationTimerStore,
    IOrchestrationQueryStore,
    IOrchestrationAdministrationStore
    where TContext : DbContext, IOrchestrationContext
{
    private const long HistoryTimestampIncrementTicks = 10; // 1 microsecond
    private readonly IServiceProvider serviceProvider;
    private readonly IOrchestrationClock clock;
    private readonly ILogger<EntityFrameworkOrchestrationStorageProvider<TContext>> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkOrchestrationStorageProvider{TContext}"/> class.
    /// </summary>
    /// <param name="serviceProvider">The root service provider.</param>
    /// <param name="options">The provider options.</param>
    /// <param name="clock">The orchestration clock.</param>
    public EntityFrameworkOrchestrationStorageProvider(
        IServiceProvider serviceProvider,
        EntityFrameworkOrchestrationOptions options,
        IOrchestrationClock clock = null,
        ILoggerFactory loggerFactory = null)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.Options = options ?? throw new ArgumentNullException(nameof(options));
        this.clock = clock ?? new SystemOrchestrationClock();
        this.logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<EntityFrameworkOrchestrationStorageProvider<TContext>>();
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

    /// <summary>
    /// Gets the provider options.
    /// </summary>
    public EntityFrameworkOrchestrationOptions Options { get; }

    /// <inheritdoc />
    public ISerializer Serializer => this.Options.Serializer;

    /// <inheritdoc />
    public async Task<OrchestrationInstanceSnapshot> CreateAsync<TData>(
        OrchestrationContext<TData> context,
        string concurrencyKey = null,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(context.InstanceId, context.OrchestrationName, context.CurrentState);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        if (await dbContext.OrchestrationInstances.AsNoTracking()
            .AnyAsync(item => item.InstanceId == context.InstanceId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Orchestration instance '{context.InstanceId}' already exists.");
        }

        var now = this.clock.UtcNow;
        var actor = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        var row = new OrchestrationInstance
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
            Version = 1,
            IsArchived = false,
            ArchivedUtc = null,
            CreatedDate = now,
            UpdatedDate = now,
            CreatedBy = actor,
            UpdatedBy = actor,
        };

        dbContext.OrchestrationInstances.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        this.logger.LogDebug(
            "{LogKey} orchestration instance persisted (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, orchestration={Orchestration}, status={Status}, version={Version})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.InstanceId,
            row.OrchestrationName,
            row.Status,
            row.Version);

        return this.ToSnapshot(row);
    }

    /// <inheritdoc />
    public async Task<OrchestrationInstanceSnapshot> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationInstances.AsNoTracking()
            .SingleOrDefaultAsync(item => item.InstanceId == instanceId, cancellationToken)
            .ConfigureAwait(false);

        return this.ToSnapshot(row);
    }

    /// <inheritdoc />
    public async Task<OrchestrationInstanceSnapshot> SaveAsync<TData>(
        OrchestrationInstanceSnapshot snapshot,
        OrchestrationContext<TData> context,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(snapshot.InstanceId, context.OrchestrationName, context.CurrentState);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationInstances
            .SingleOrDefaultAsync(item => item.InstanceId == snapshot.InstanceId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new KeyNotFoundException($"Orchestration instance '{snapshot.InstanceId}' was not found.");
        }

        if (row.Version != snapshot.Version)
        {
            throw new InvalidOperationException(
                $"Orchestration instance '{snapshot.InstanceId}' version mismatch. Expected '{row.Version}', received '{snapshot.Version}'.");
        }

        var now = this.clock.UtcNow;
        row.OrchestrationName = context.OrchestrationName;
        row.Status = context.Status;
        row.CurrentState = context.CurrentState;
        row.CurrentActivity = context.CurrentActivity;
        row.CorrelationId = context.CorrelationId;
        row.StartedUtc = context.StartedUtc;
        row.CompletedUtc = context.CompletedUtc;
        row.ContextType = typeof(TData).AssemblyQualifiedName;
        row.SerializedContext = this.Serializer.SerializeToString(this.CaptureContext(context));
        row.Version += 1;
        row.UpdatedDate = now;
        row.UpdatedBy = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        row.AdvanceConcurrencyVersion();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            this.logger.LogWarning(
                exception,
                "{LogKey} orchestration snapshot persistence hit a concurrency conflict (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, expectedVersion={ExpectedVersion}, currentVersion={CurrentVersion})",
                Constants.LogKey,
                typeof(TContext).Name,
                snapshot.InstanceId,
                snapshot.Version,
                row.Version);
            throw new InvalidOperationException(
                $"Orchestration instance '{snapshot.InstanceId}' version mismatch. The snapshot is no longer current.",
                exception);
        }

        this.logger.LogDebug(
            "{LogKey} orchestration snapshot persisted (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, status={Status}, state={State}, activity={Activity}, version={Version})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.InstanceId,
            row.Status,
            row.CurrentState,
            row.CurrentActivity,
            row.Version);

        return this.ToSnapshot(row);
    }

    /// <inheritdoc />
    public async Task<OrchestrationLease> AcquireAsync(
        Guid instanceId,
        string owner,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(instanceId);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationInstances
            .SingleOrDefaultAsync(item => item.InstanceId == instanceId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
        }

        var now = this.clock.UtcNow;
        if (row.LeaseExpiresUtc.HasValue && row.LeaseExpiresUtc.Value > now)
        {
            throw new InvalidOperationException($"Orchestration instance '{instanceId}' is already leased to '{row.LeaseOwner}'.");
        }

        var leaseId = Guid.NewGuid();
        row.LeaseId = leaseId;
        row.LeaseOwner = owner;
        row.LeaseAcquiredUtc = now;
        row.LeaseExpiresUtc = now.Add(duration);
        row.UpdatedDate = now;
        row.UpdatedBy = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        row.AdvanceConcurrencyVersion();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            this.logger.LogDebug(
                exception,
                "{LogKey} orchestration lease acquisition lost due to concurrency (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, owner={Owner})",
                Constants.LogKey,
                typeof(TContext).Name,
                instanceId,
                owner);
            throw new InvalidOperationException($"Orchestration instance '{instanceId}' is already leased by another worker.", exception);
        }

        this.logger.LogDebug(
            "{LogKey} orchestration lease acquired (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, owner={Owner}, leaseId={LeaseId}, expiresUtc={ExpiresUtc})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.InstanceId,
            owner,
            leaseId,
            row.LeaseExpiresUtc);

        return new OrchestrationLease
        {
            LeaseId = leaseId,
            InstanceId = row.InstanceId,
            Owner = owner,
            AcquiredUtc = row.LeaseAcquiredUtc!.Value,
            ExpiresUtc = row.LeaseExpiresUtc!.Value,
        };
    }

    /// <inheritdoc />
    public async Task<OrchestrationLease> RenewAsync(
        Guid instanceId,
        Guid leaseId,
        string owner,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(instanceId);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationInstances
            .SingleOrDefaultAsync(item => item.InstanceId == instanceId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null ||
            row.LeaseId != leaseId ||
            !string.Equals(row.LeaseOwner, owner, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Lease '{leaseId}' for orchestration instance '{instanceId}' is not owned by '{owner}'.");
        }

        var now = this.clock.UtcNow;
        if (!row.LeaseExpiresUtc.HasValue || row.LeaseExpiresUtc.Value <= now)
        {
            throw new InvalidOperationException($"Lease '{leaseId}' for orchestration instance '{instanceId}' has already expired.");
        }

        row.LeaseExpiresUtc = now.Add(duration);
        row.UpdatedDate = now;
        row.UpdatedBy = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        row.AdvanceConcurrencyVersion();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            this.logger.LogWarning(
                exception,
                "{LogKey} orchestration lease renewal lost due to concurrency (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, owner={Owner}, leaseId={LeaseId})",
                Constants.LogKey,
                typeof(TContext).Name,
                instanceId,
                owner,
                leaseId);
            throw new InvalidOperationException($"Lease '{leaseId}' for orchestration instance '{instanceId}' is no longer current.", exception);
        }

        this.logger.LogDebug(
            "{LogKey} orchestration lease renewed (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, owner={Owner}, leaseId={LeaseId}, expiresUtc={ExpiresUtc})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.InstanceId,
            owner,
            leaseId,
            row.LeaseExpiresUtc);

        return new OrchestrationLease
        {
            LeaseId = row.LeaseId!.Value,
            InstanceId = row.InstanceId,
            Owner = row.LeaseOwner,
            AcquiredUtc = row.LeaseAcquiredUtc!.Value,
            ExpiresUtc = row.LeaseExpiresUtc!.Value,
        };
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(instanceId);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationInstances
            .SingleOrDefaultAsync(item => item.InstanceId == instanceId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null ||
            row.LeaseId != leaseId ||
            !string.Equals(row.LeaseOwner, owner, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Lease '{leaseId}' for orchestration instance '{instanceId}' is not owned by '{owner}'.");
        }

        var now = this.clock.UtcNow;
        row.LeaseId = null;
        row.LeaseOwner = null;
        row.LeaseAcquiredUtc = null;
        row.LeaseExpiresUtc = null;
        row.UpdatedDate = now;
        row.UpdatedBy = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        row.AdvanceConcurrencyVersion();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            this.logger.LogWarning(
                exception,
                "{LogKey} orchestration lease release lost due to concurrency (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, owner={Owner}, leaseId={LeaseId})",
                Constants.LogKey,
                typeof(TContext).Name,
                instanceId,
                owner,
                leaseId);
            throw new InvalidOperationException($"Lease '{leaseId}' for orchestration instance '{instanceId}' is no longer current.", exception);
        }

        this.logger.LogDebug(
            "{LogKey} orchestration lease released (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, owner={Owner}, leaseId={LeaseId})",
            Constants.LogKey,
            typeof(TContext).Name,
            instanceId,
            owner,
            leaseId);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = this.clock.UtcNow;
        var row = await dbContext.OrchestrationInstances.AsNoTracking()
            .SingleOrDefaultAsync(item => item.InstanceId == instanceId, cancellationToken)
            .ConfigureAwait(false);

        return row is not null &&
            row.LeaseId == leaseId &&
            string.Equals(row.LeaseOwner, owner, StringComparison.Ordinal) &&
            row.LeaseExpiresUtc.HasValue &&
            row.LeaseExpiresUtc.Value > now;
    }

    /// <inheritdoc />
    public async Task<OrchestrationHistoryEntry> AppendAsync(OrchestrationHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(entry.InstanceId, stateName: entry.StateName);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var actor = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        var recordedAt = entry.RecordedAt == default ? this.clock.UtcNow : entry.RecordedAt;
        var latestRecordedAt = (await dbContext.OrchestrationHistory.AsNoTracking()
            .Where(item => item.InstanceId == entry.InstanceId)
            .Select(item => (DateTimeOffset?)item.RecordedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .OrderByDescending(item => item)
            .FirstOrDefault();

        if (latestRecordedAt.HasValue && recordedAt <= latestRecordedAt.Value)
        {
            recordedAt = latestRecordedAt.Value.AddTicks(HistoryTimestampIncrementTicks);
            this.logger.LogDebug(
                "{LogKey} adjusted orchestration history timestamp to preserve ordering (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, eventType={EventType}, adjustedRecordedAt={RecordedAt})",
                Constants.LogKey,
                typeof(TContext).Name,
                entry.InstanceId,
                entry.EventType,
                recordedAt);
        }

        var row = new OrchestrationHistory
        {
            EntryId = entry.EntryId == Guid.Empty ? Guid.NewGuid() : entry.EntryId,
            InstanceId = entry.InstanceId,
            EventType = entry.EventType,
            StateName = entry.StateName,
            ActivityName = entry.ActivityName,
            Details = entry.Details,
            RecordedAt = recordedAt,
            RecordedBy = string.IsNullOrWhiteSpace(entry.RecordedBy) ? actor : entry.RecordedBy,
        };

        dbContext.OrchestrationHistory.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        this.logger.LogDebug(
            "{LogKey} orchestration history appended (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, entryId={EntryId}, eventType={EventType}, state={State}, activity={Activity})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.InstanceId,
            row.EntryId,
            row.EventType,
            row.StateName,
            row.ActivityName);

        return this.ToHistoryEntry(row);
    }

    /// <inheritdoc />
    async Task<IReadOnlyCollection<OrchestrationHistoryEntry>> IOrchestrationHistoryStore.GetAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = dbContext.OrchestrationHistory.AsNoTracking()
            .Where(item => item.InstanceId == instanceId);

        var rows = IsSqlite(dbContext)
            ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(item => item.RecordedAt)
                .ThenBy(item => item.EntryId)
                .ToArray()
            : await query
                .OrderBy(item => item.RecordedAt)
                .ThenBy(item => item.EntryId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

        return rows.Select(this.ToHistoryEntry).ToArray();
    }

    /// <inheritdoc />
    public async Task<OrchestrationSignalRecord> PersistAsync<TPayload>(
        Guid instanceId,
        string signalName,
        TPayload payload,
        string currentState = null,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(instanceId, stateName: currentState);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var duplicate = await dbContext.OrchestrationSignals.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.InstanceId == instanceId &&
                        item.SignalName == signalName &&
                        item.IdempotencyKey == idempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (duplicate is not null)
            {
                this.logger.LogDebug(
                    "{LogKey} orchestration signal deduplicated (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, signal={SignalName}, signalId={SignalId}, idempotencyKey={IdempotencyKey})",
                    Constants.LogKey,
                    typeof(TContext).Name,
                    instanceId,
                    signalName,
                    duplicate.SignalId,
                    idempotencyKey);
                return this.ToSignalRecord(duplicate);
            }
        }

        var now = this.clock.UtcNow;
        var actor = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        var row = new OrchestrationSignal
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

        dbContext.OrchestrationSignals.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        this.logger.LogDebug(
            "{LogKey} orchestration signal persisted (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, signalId={SignalId}, signal={SignalName}, state={State}, status={Status})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.InstanceId,
            row.SignalId,
            row.SignalName,
            row.CurrentState,
            row.Status);

        return this.ToSignalRecord(row);
    }

    /// <inheritdoc />
    async Task<IReadOnlyCollection<OrchestrationSignalRecord>> IOrchestrationSignalStore.GetAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = dbContext.OrchestrationSignals.AsNoTracking()
            .Where(item => item.InstanceId == instanceId);

        var rows = IsSqlite(dbContext)
            ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(item => item.ReceivedUtc)
                .ThenBy(item => item.SignalId)
                .ToArray()
            : await query
                .OrderBy(item => item.ReceivedUtc)
                .ThenBy(item => item.SignalId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

        return rows.Select(this.ToSignalRecord).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OrchestrationSignalRecord>> GetProcessableAsync(
        Guid instanceId,
        string currentState,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = dbContext.OrchestrationSignals.AsNoTracking()
            .Where(item => item.InstanceId == instanceId)
            .Where(item => item.Status == OrchestrationSignalStatus.Pending);

        OrchestrationSignal[] rows;
        if (IsSqlite(dbContext))
        {
            rows = (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                .Where(item => string.IsNullOrWhiteSpace(item.CurrentState) || string.Equals(item.CurrentState, currentState, StringComparison.Ordinal))
                .OrderBy(item => item.ReceivedUtc)
                .ThenBy(item => item.SignalId)
                .ToArray();
        }
        else
        {
            rows = await query
                .Where(item => item.CurrentState == null || item.CurrentState == string.Empty || item.CurrentState == currentState)
                .OrderBy(item => item.ReceivedUtc)
                .ThenBy(item => item.SignalId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return rows.Select(this.ToSignalRecord).ToArray();
    }

    /// <inheritdoc />
    public async Task<OrchestrationSignalRecord> UpdateStatusAsync(
        Guid signalId,
        OrchestrationSignalStatus status,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationSignals
            .SingleOrDefaultAsync(item => item.SignalId == signalId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new KeyNotFoundException($"Signal '{signalId}' was not found.");
        }
        using var logScope = this.BeginOrchestrationScope(row.InstanceId, stateName: row.CurrentState);

        var now = this.clock.UtcNow;
        row.Status = status;
        row.StatusReason = reason;
        row.ProcessedUtc = status == OrchestrationSignalStatus.Pending ? row.ProcessedUtc : now;
        row.UpdatedDate = now;
        row.UpdatedBy = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        row.AdvanceConcurrencyVersion();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            this.logger.LogWarning(
                exception,
                "{LogKey} orchestration signal status update lost due to concurrency (provider=EntityFramework, context={DbContextType}, signalId={SignalId}, status={Status})",
                Constants.LogKey,
                typeof(TContext).Name,
                signalId,
                status);
            throw new InvalidOperationException($"Signal '{signalId}' is no longer current.", exception);
        }

        this.logger.LogDebug(
            "{LogKey} orchestration signal status updated (provider=EntityFramework, context={DbContextType}, signalId={SignalId}, instanceId={InstanceId}, signal={SignalName}, status={Status}, reason={Reason})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.SignalId,
            row.InstanceId,
            row.SignalName,
            row.Status,
            row.StatusReason);

        return this.ToSignalRecord(row);
    }

    /// <inheritdoc />
    public async Task<OrchestrationTimerRecord> ScheduleAsync(
        Guid instanceId,
        string triggerKind,
        DateTimeOffset dueTime,
        string targetState = null,
        string continuation = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerKind);
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(instanceId, stateName: targetState);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = this.clock.UtcNow;
        var actor = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        var row = new OrchestrationTimer
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

        dbContext.OrchestrationTimers.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        this.logger.LogDebug(
            "{LogKey} orchestration timer persisted (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, timerId={TimerId}, trigger={TriggerKind}, dueTimeUtc={DueTimeUtc}, continuation={Continuation}, targetState={TargetState})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.InstanceId,
            row.TimerId,
            row.TriggerKind,
            row.DueTimeUtc,
            row.Continuation,
            row.TargetState);

        return this.ToTimerRecord(row);
    }

    /// <inheritdoc />
    async Task<IReadOnlyCollection<OrchestrationTimerRecord>> IOrchestrationTimerStore.GetAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = dbContext.OrchestrationTimers.AsNoTracking()
            .Where(item => item.InstanceId == instanceId);

        var rows = IsSqlite(dbContext)
            ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(item => item.DueTimeUtc)
                .ThenBy(item => item.CreatedDate)
                .ThenBy(item => item.TimerId)
                .ToArray()
            : await query
                .OrderBy(item => item.DueTimeUtc)
                .ThenBy(item => item.CreatedDate)
                .ThenBy(item => item.TimerId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

        return rows.Select(this.ToTimerRecord).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OrchestrationTimerRecord>> GetDueAsync(
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = dbContext.OrchestrationTimers.AsNoTracking()
            .Where(item => item.Status == OrchestrationTimerStatus.Pending);

        OrchestrationTimer[] rows;
        if (IsSqlite(dbContext))
        {
            rows = (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                .Where(item => item.DueTimeUtc <= asOfUtc)
                .OrderBy(item => item.DueTimeUtc)
                .ThenBy(item => item.CreatedDate)
                .ThenBy(item => item.TimerId)
                .ToArray();
        }
        else
        {
            rows = await query
                .Where(item => item.DueTimeUtc <= asOfUtc)
                .OrderBy(item => item.DueTimeUtc)
                .ThenBy(item => item.CreatedDate)
                .ThenBy(item => item.TimerId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return rows.Select(this.ToTimerRecord).ToArray();
    }

    /// <inheritdoc />
    public async Task<OrchestrationTimerRecord> UpdateStatusAsync(
        Guid timerId,
        OrchestrationTimerStatus status,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationTimers
            .SingleOrDefaultAsync(item => item.TimerId == timerId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new KeyNotFoundException($"Timer '{timerId}' was not found.");
        }
        using var logScope = this.BeginOrchestrationScope(row.InstanceId, stateName: row.TargetState);

        var now = this.clock.UtcNow;
        row.Status = status;
        row.StatusReason = reason;
        row.ProcessedUtc = status == OrchestrationTimerStatus.Pending ? row.ProcessedUtc : now;
        row.UpdatedDate = now;
        row.UpdatedBy = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        row.AdvanceConcurrencyVersion();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            this.logger.LogWarning(
                exception,
                "{LogKey} orchestration timer status update lost due to concurrency (provider=EntityFramework, context={DbContextType}, timerId={TimerId}, status={Status})",
                Constants.LogKey,
                typeof(TContext).Name,
                timerId,
                status);
            throw new InvalidOperationException($"Timer '{timerId}' is no longer current.", exception);
        }

        this.logger.LogDebug(
            "{LogKey} orchestration timer status updated (provider=EntityFramework, context={DbContextType}, timerId={TimerId}, instanceId={InstanceId}, trigger={TriggerKind}, status={Status}, reason={Reason})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.TimerId,
            row.InstanceId,
            row.TriggerKind,
            row.Status,
            row.StatusReason);

        return this.ToTimerRecord(row);
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
    public async Task<OrchestrationInstanceQueryResult> QueryAsync(
        OrchestrationInstanceQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        query ??= new OrchestrationInstanceQuery();
        using var logScope = this.BeginProviderScope();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        if (IsSqlite(dbContext))
        {
            var sqliteRows = await dbContext.OrchestrationInstances.AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
            var items = sqliteRows.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.OrchestrationName))
            {
                items = items.Where(item => string.Equals(item.OrchestrationName, query.OrchestrationName, StringComparison.Ordinal));
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
                items = items.Where(item => string.Equals(item.CorrelationId, query.CorrelationId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(query.ConcurrencyKey))
            {
                items = items.Where(item => string.Equals(item.ConcurrencyKey, query.ConcurrencyKey, StringComparison.Ordinal));
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

            var sqliteTotal = items.Count();
            var paged = items
                .OrderByDescending(item => item.StartedUtc)
                .ThenBy(item => item.InstanceId)
                .Skip(Math.Max(0, query.Skip))
                .Take(query.Take <= 0 ? 100 : query.Take)
                .Select(this.ToSnapshot)
                .ToArray();

            var result = new OrchestrationInstanceQueryResult
            {
                TotalCount = sqliteTotal,
                Items = paged,
            };

            this.logger.LogDebug(
                "{LogKey} orchestration instances queried (provider=EntityFramework, context={DbContextType}, total={TotalCount}, returned={ReturnedCount}, orchestration={Orchestration}, skip={Skip}, take={Take})",
                Constants.LogKey,
                typeof(TContext).Name,
                result.TotalCount,
                result.Items.Count,
                query.OrchestrationName,
                query.Skip,
                query.Take);

            return result;
        }

        var instances = dbContext.OrchestrationInstances.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.OrchestrationName))
        {
            instances = instances.Where(item => item.OrchestrationName == query.OrchestrationName);
        }

        if (query.Statuses is { Count: > 0 })
        {
            var statusSet = new HashSet<OrchestrationStatus>(query.Statuses);
            instances = instances.Where(item => statusSet.Contains(item.Status));
        }

        if (query.States is { Count: > 0 })
        {
            var stateSet = new HashSet<string>(query.States, StringComparer.OrdinalIgnoreCase);
            instances = instances.Where(item => stateSet.Contains(item.CurrentState ?? string.Empty));
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            instances = instances.Where(item => item.CorrelationId == query.CorrelationId);
        }

        if (!string.IsNullOrWhiteSpace(query.ConcurrencyKey))
        {
            instances = instances.Where(item => item.ConcurrencyKey == query.ConcurrencyKey);
        }

        if (query.StartedFromUtc.HasValue)
        {
            instances = instances.Where(item => item.StartedUtc >= query.StartedFromUtc.Value);
        }

        if (query.StartedToUtc.HasValue)
        {
            instances = instances.Where(item => item.StartedUtc <= query.StartedToUtc.Value);
        }

        if (query.CompletedFromUtc.HasValue)
        {
            instances = instances.Where(item => item.CompletedUtc.HasValue && item.CompletedUtc.Value >= query.CompletedFromUtc.Value);
        }

        if (query.CompletedToUtc.HasValue)
        {
            instances = instances.Where(item => item.CompletedUtc.HasValue && item.CompletedUtc.Value <= query.CompletedToUtc.Value);
        }

        var total = await instances.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await instances
            .OrderByDescending(item => item.StartedUtc)
            .ThenBy(item => item.InstanceId)
            .Skip(Math.Max(0, query.Skip))
            .Take(query.Take <= 0 ? 100 : query.Take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var queried = new OrchestrationInstanceQueryResult
        {
            TotalCount = total,
            Items = rows.Select(this.ToSnapshot).ToArray(),
        };

        this.logger.LogDebug(
            "{LogKey} orchestration instances queried (provider=EntityFramework, context={DbContextType}, total={TotalCount}, returned={ReturnedCount}, orchestration={Orchestration}, skip={Skip}, take={Take})",
            Constants.LogKey,
            typeof(TContext).Name,
            queried.TotalCount,
            queried.Items.Count,
            query.OrchestrationName,
            query.Skip,
            query.Take);

        return queried;
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
    public async Task<OrchestrationMetricsSnapshot> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginProviderScope();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        if (IsSqlite(dbContext))
        {
            var sqliteInstances = await dbContext.OrchestrationInstances.AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
            var sqliteCountsByOrchestration = sqliteInstances
                .GroupBy(item => item.OrchestrationName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            var metrics = new OrchestrationMetricsSnapshot
            {
                TotalInstances = sqliteInstances.Length,
                RunningInstances = sqliteInstances.Count(item => item.Status == OrchestrationStatus.Running),
                WaitingInstances = sqliteInstances.Count(item => item.Status == OrchestrationStatus.Waiting),
                PausedInstances = sqliteInstances.Count(item => item.Status == OrchestrationStatus.Paused),
                CompletedInstances = sqliteInstances.Count(item => item.Status == OrchestrationStatus.Completed),
                CancelledInstances = sqliteInstances.Count(item => item.Status == OrchestrationStatus.Cancelled),
                FailedInstances = sqliteInstances.Count(item => item.Status == OrchestrationStatus.Failed),
                TerminatedInstances = sqliteInstances.Count(item => item.Status == OrchestrationStatus.Terminated),
                OldestWaitingStartedUtc = sqliteInstances
                    .Where(item => item.Status == OrchestrationStatus.Waiting)
                    .OrderBy(item => item.StartedUtc)
                    .Select(item => (DateTimeOffset?)item.StartedUtc)
                    .FirstOrDefault(),
                InstanceCountsByOrchestration = sqliteCountsByOrchestration,
                HistoryCount = await dbContext.OrchestrationHistory.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false),
                SignalCount = await dbContext.OrchestrationSignals.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false),
                TimerCount = await dbContext.OrchestrationTimers.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false),
            };

            this.logger.LogDebug(
                "{LogKey} orchestration metrics queried (provider=EntityFramework, context={DbContextType}, totalInstances={TotalInstances}, waitingInstances={WaitingInstances}, timerCount={TimerCount})",
                Constants.LogKey,
                typeof(TContext).Name,
                metrics.TotalInstances,
                metrics.WaitingInstances,
                metrics.TimerCount);

            return metrics;
        }

        var instances = dbContext.OrchestrationInstances.AsNoTracking();

        var countsByOrchestration = await instances
            .GroupBy(item => item.OrchestrationName)
            .Select(group => new { OrchestrationName = group.Key ?? string.Empty, Count = group.Count() })
            .ToDictionaryAsync(item => item.OrchestrationName, item => item.Count, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var snapshot = new OrchestrationMetricsSnapshot
        {
            TotalInstances = await instances.CountAsync(cancellationToken).ConfigureAwait(false),
            RunningInstances = await instances.CountAsync(item => item.Status == OrchestrationStatus.Running, cancellationToken).ConfigureAwait(false),
            WaitingInstances = await instances.CountAsync(item => item.Status == OrchestrationStatus.Waiting, cancellationToken).ConfigureAwait(false),
            PausedInstances = await instances.CountAsync(item => item.Status == OrchestrationStatus.Paused, cancellationToken).ConfigureAwait(false),
            CompletedInstances = await instances.CountAsync(item => item.Status == OrchestrationStatus.Completed, cancellationToken).ConfigureAwait(false),
            CancelledInstances = await instances.CountAsync(item => item.Status == OrchestrationStatus.Cancelled, cancellationToken).ConfigureAwait(false),
            FailedInstances = await instances.CountAsync(item => item.Status == OrchestrationStatus.Failed, cancellationToken).ConfigureAwait(false),
            TerminatedInstances = await instances.CountAsync(item => item.Status == OrchestrationStatus.Terminated, cancellationToken).ConfigureAwait(false),
            OldestWaitingStartedUtc = await instances
                .Where(item => item.Status == OrchestrationStatus.Waiting)
                .OrderBy(item => item.StartedUtc)
                .Select(item => (DateTimeOffset?)item.StartedUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
            InstanceCountsByOrchestration = countsByOrchestration,
            HistoryCount = await dbContext.OrchestrationHistory.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false),
            SignalCount = await dbContext.OrchestrationSignals.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false),
            TimerCount = await dbContext.OrchestrationTimers.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false),
        };

        this.logger.LogDebug(
            "{LogKey} orchestration metrics queried (provider=EntityFramework, context={DbContextType}, totalInstances={TotalInstances}, waitingInstances={WaitingInstances}, timerCount={TimerCount})",
            Constants.LogKey,
            typeof(TContext).Name,
            snapshot.TotalInstances,
            snapshot.WaitingInstances,
            snapshot.TimerCount);

        return snapshot;
    }

    /// <inheritdoc />
    public async Task<bool> ArchiveAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(instanceId);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationInstances
            .SingleOrDefaultAsync(item => item.InstanceId == instanceId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
        }

        if (row.IsArchived)
        {
            return false;
        }

        if (row.Status is not (OrchestrationStatus.Completed or OrchestrationStatus.Cancelled or OrchestrationStatus.Terminated or OrchestrationStatus.Failed))
        {
            throw new InvalidOperationException($"Orchestration instance '{instanceId}' is not archivable in its current state.");
        }

        var now = this.clock.UtcNow;
        var actor = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        row.IsArchived = true;
        row.ArchivedUtc = now;
        row.Version += 1;
        row.UpdatedDate = now;
        row.UpdatedBy = actor;
        row.AdvanceConcurrencyVersion();

        dbContext.OrchestrationHistory.Add(this.CreateHistoryRow(instanceId, "Archived", row.CurrentState, row.CurrentActivity, "Administrative archive", now, actor));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        this.logger.LogInformation(
            "{LogKey} orchestration instance archived (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, status={Status}, archivedUtc={ArchivedUtc})",
            Constants.LogKey,
            typeof(TContext).Name,
            row.InstanceId,
            row.Status,
            row.ArchivedUtc);

        return true;
    }

    /// <inheritdoc />
    public async Task<OrchestrationPurgeResult> PurgeAsync(OrchestrationPurgeCriteria request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request ??= new OrchestrationPurgeCriteria();
        using var logScope = this.BeginProviderScope();

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var instances = await dbContext.OrchestrationInstances.ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var candidateIds = instances
            .Where(item => !request.IsArchived.HasValue || item.IsArchived == request.IsArchived.Value)
            .Where(item => request.Statuses.Count == 0 || request.Statuses.Contains(item.Status))
            .Where(item => !request.OlderThan.HasValue || GetRetentionTimestamp(item) <= request.OlderThan.Value)
            .Select(item => item.InstanceId)
            .ToArray();

        if (candidateIds.Length == 0)
        {
            return new OrchestrationPurgeResult();
        }

        var instanceRows = dbContext.OrchestrationInstances.Where(item => candidateIds.Contains(item.InstanceId));
        var historyRows = dbContext.OrchestrationHistory.Where(item => candidateIds.Contains(item.InstanceId));
        var signalRows = dbContext.OrchestrationSignals.Where(item => candidateIds.Contains(item.InstanceId));
        var timerRows = dbContext.OrchestrationTimers.Where(item => candidateIds.Contains(item.InstanceId));

        var purgedHistoryCount = await historyRows.CountAsync(cancellationToken).ConfigureAwait(false);
        var purgedSignalCount = await signalRows.CountAsync(cancellationToken).ConfigureAwait(false);
        var purgedTimerCount = await timerRows.CountAsync(cancellationToken).ConfigureAwait(false);

        dbContext.OrchestrationHistory.RemoveRange(await historyRows.ToArrayAsync(cancellationToken).ConfigureAwait(false));
        dbContext.OrchestrationSignals.RemoveRange(await signalRows.ToArrayAsync(cancellationToken).ConfigureAwait(false));
        dbContext.OrchestrationTimers.RemoveRange(await timerRows.ToArrayAsync(cancellationToken).ConfigureAwait(false));
        dbContext.OrchestrationInstances.RemoveRange(await instanceRows.ToArrayAsync(cancellationToken).ConfigureAwait(false));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        this.logger.LogInformation(
            "{LogKey} orchestration data purged (provider=EntityFramework, context={DbContextType}, instances={InstanceCount}, history={HistoryCount}, signals={SignalCount}, timers={TimerCount})",
            Constants.LogKey,
            typeof(TContext).Name,
            candidateIds.Length,
            purgedHistoryCount,
            purgedSignalCount,
            purgedTimerCount);

        return new OrchestrationPurgeResult
        {
            PurgedInstanceCount = candidateIds.Length,
            PurgedHistoryCount = purgedHistoryCount,
            PurgedSignalCount = purgedSignalCount,
            PurgedTimerCount = purgedTimerCount,
        };
    }

    /// <inheritdoc />
    public async Task ReleaseLeaseAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(instanceId);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationInstances
            .SingleOrDefaultAsync(item => item.InstanceId == instanceId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
        }

        if (!row.LeaseId.HasValue || !row.LeaseExpiresUtc.HasValue || row.LeaseExpiresUtc.Value <= this.clock.UtcNow)
        {
            throw new InvalidOperationException($"Orchestration instance '{instanceId}' does not have an active lease.");
        }

        var now = this.clock.UtcNow;
        var actor = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        row.LeaseId = null;
        row.LeaseOwner = null;
        row.LeaseAcquiredUtc = null;
        row.LeaseExpiresUtc = null;
        row.Version += 1;
        row.UpdatedDate = now;
        row.UpdatedBy = actor;
        row.AdvanceConcurrencyVersion();

        dbContext.OrchestrationHistory.Add(this.CreateHistoryRow(instanceId, "LeaseReleased", row.CurrentState, row.CurrentActivity, "Administrative lease release", now, actor));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        this.logger.LogInformation(
            "{LogKey} orchestration lease released administratively (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId})",
            Constants.LogKey,
            typeof(TContext).Name,
            instanceId);
    }

    /// <inheritdoc />
    public async Task<int> RequeueTimersAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var logScope = this.BeginOrchestrationScope(instanceId);

        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var row = await dbContext.OrchestrationInstances
            .SingleOrDefaultAsync(item => item.InstanceId == instanceId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found.");
        }

        if (row.Status is OrchestrationStatus.Completed or OrchestrationStatus.Cancelled or OrchestrationStatus.Terminated or OrchestrationStatus.Failed)
        {
            throw new InvalidOperationException($"Orchestration instance '{instanceId}' is already terminal.");
        }

        var timers = await dbContext.OrchestrationTimers
            .Where(item => item.InstanceId == instanceId)
            .Where(item => item.Status != OrchestrationTimerStatus.Pending)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        if (timers.Length == 0)
        {
            throw new InvalidOperationException($"Orchestration instance '{instanceId}' does not have requeueable timers.");
        }

        var now = this.clock.UtcNow;
        var actor = this.GetCurrentActor(scope.ServiceProvider.GetService<ICurrentUserAccessor>());
        foreach (var timer in timers)
        {
            timer.Status = OrchestrationTimerStatus.Pending;
            timer.DueTimeUtc = now;
            timer.ProcessedUtc = null;
            timer.StatusReason = "Requeued by administration.";
            timer.UpdatedDate = now;
            timer.UpdatedBy = actor;
            timer.AdvanceConcurrencyVersion();
        }

        row.Version += 1;
        row.UpdatedDate = now;
        row.UpdatedBy = actor;
        row.AdvanceConcurrencyVersion();

        dbContext.OrchestrationHistory.Add(this.CreateHistoryRow(instanceId, "TimersRequeued", row.CurrentState, row.CurrentActivity, timers.Length.ToString(), now, actor));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        this.logger.LogInformation(
            "{LogKey} orchestration timers requeued administratively (provider=EntityFramework, context={DbContextType}, instanceId={InstanceId}, timerCount={TimerCount})",
            Constants.LogKey,
            typeof(TContext).Name,
            instanceId,
            timers.Length);

        return timers.Length;
    }

    private IDisposable BeginProviderScope()
    {
        return this.logger.BeginScope(new Dictionary<string, object>
        {
            ["OrchestrationStorageProvider"] = "EntityFramework",
            ["DbContextType"] = typeof(TContext).Name,
        });
    }

    private IDisposable BeginOrchestrationScope(Guid instanceId, string orchestrationName = null, string stateName = null)
    {
        var state = new Dictionary<string, object>
        {
            ["OrchestrationStorageProvider"] = "EntityFramework",
            ["DbContextType"] = typeof(TContext).Name,
            ["OrchestrationInstanceId"] = instanceId,
        };

        if (!string.IsNullOrWhiteSpace(orchestrationName))
        {
            state["OrchestrationName"] = orchestrationName;
        }

        if (!string.IsNullOrWhiteSpace(stateName))
        {
            state["OrchestrationState"] = stateName;
        }

        return this.logger.BeginScope(state);
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

    private static bool IsSqlite(DbContext dbContext)
    {
        return dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
    }

    private string GetCurrentActor(ICurrentUserAccessor currentUserAccessor)
    {
        if (currentUserAccessor is null || !currentUserAccessor.IsAuthenticated)
        {
            return null;
        }

        return currentUserAccessor.UserId ?? currentUserAccessor.UserName ?? currentUserAccessor.Email;
    }

    private OrchestrationHistory CreateHistoryRow(
        Guid instanceId,
        string eventType,
        string stateName,
        string activityName,
        string details,
        DateTimeOffset recordedAt,
        string recordedBy)
    {
        return new OrchestrationHistory
        {
            EntryId = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = eventType,
            StateName = stateName,
            ActivityName = activityName,
            Details = details,
            RecordedAt = recordedAt,
            RecordedBy = recordedBy,
        };
    }

    private static DateTimeOffset GetRetentionTimestamp(OrchestrationInstance row)
    {
        return row.ArchivedUtc ?? row.CompletedUtc ?? row.UpdatedDate;
    }

    private OrchestrationInstanceSnapshot ToSnapshot(OrchestrationInstance row)
    {
        return row is null
            ? null
            : new OrchestrationInstanceSnapshot
            {
                InstanceId = row.InstanceId,
                OrchestrationName = row.OrchestrationName,
                Status = row.Status,
                CurrentState = row.CurrentState,
                CurrentActivity = row.CurrentActivity,
                CorrelationId = row.CorrelationId,
                ConcurrencyKey = row.ConcurrencyKey,
                StartedUtc = row.StartedUtc,
                CompletedUtc = row.CompletedUtc,
                ContextType = row.ContextType,
                SerializedContext = row.SerializedContext,
                Version = row.Version,
                IsArchived = row.IsArchived,
                ArchivedUtc = row.ArchivedUtc,
                CreatedDate = row.CreatedDate,
                UpdatedDate = row.UpdatedDate,
                CreatedBy = row.CreatedBy,
                UpdatedBy = row.UpdatedBy,
            };
    }

    private OrchestrationHistoryEntry ToHistoryEntry(OrchestrationHistory row)
    {
        return row is null
            ? null
            : new OrchestrationHistoryEntry
            {
                EntryId = row.EntryId,
                InstanceId = row.InstanceId,
                EventType = row.EventType,
                StateName = row.StateName,
                ActivityName = row.ActivityName,
                Details = row.Details,
                RecordedAt = row.RecordedAt,
                RecordedBy = row.RecordedBy,
            };
    }

    private OrchestrationSignalRecord ToSignalRecord(OrchestrationSignal row)
    {
        return row is null
            ? null
            : new OrchestrationSignalRecord
            {
                SignalId = row.SignalId,
                InstanceId = row.InstanceId,
                SignalName = row.SignalName,
                CurrentState = row.CurrentState,
                Payload = row.Payload,
                PayloadType = row.PayloadType,
                IdempotencyKey = row.IdempotencyKey,
                Status = row.Status,
                ReceivedUtc = row.ReceivedUtc,
                ProcessedUtc = row.ProcessedUtc,
                StatusReason = row.StatusReason,
                CreatedDate = row.CreatedDate,
                UpdatedDate = row.UpdatedDate,
                CreatedBy = row.CreatedBy,
                UpdatedBy = row.UpdatedBy,
            };
    }

    private OrchestrationTimerRecord ToTimerRecord(OrchestrationTimer row)
    {
        return row is null
            ? null
            : new OrchestrationTimerRecord
            {
                TimerId = row.TimerId,
                InstanceId = row.InstanceId,
                TriggerKind = row.TriggerKind,
                DueTimeUtc = row.DueTimeUtc,
                TargetState = row.TargetState,
                Continuation = row.Continuation,
                Status = row.Status,
                ProcessedUtc = row.ProcessedUtc,
                StatusReason = row.StatusReason,
                CreatedDate = row.CreatedDate,
                UpdatedDate = row.UpdatedDate,
                CreatedBy = row.CreatedBy,
                UpdatedBy = row.UpdatedBy,
            };
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public static NullServiceProvider Instance { get; } = new();

        public object GetService(Type serviceType)
        {
            return null;
        }
    }
}
