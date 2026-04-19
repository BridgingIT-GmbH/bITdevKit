// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Diagnostics;
using Domain;
using Domain.Outbox;
using Microsoft.Data.SqlClient;
using Constants = Constants;

/// <summary>
/// Processes persisted domain events from the Entity Framework outbox.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IOutboxDomainEventContext" />.</typeparam>
public partial class OutboxDomainEventWorker<TContext> : IOutboxDomainEventWorker
    where TContext : DbContext, IOutboxDomainEventContext
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<OutboxDomainEventWorker<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly OutboxDomainEventOptions options;
    private readonly IEnumerable<ActivitySource> activitySources;
    private readonly string contextTypeName;
    private readonly string instanceName = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    /// <summary>
    /// Initializes a new worker instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used for diagnostics and domain event publishing.</param>
    /// <param name="serviceProvider">The service provider used to create worker scopes and resolve dependencies.</param>
    /// <param name="activitySources">The activity sources used for tracing published domain events.</param>
    /// <param name="options">The outbox processing options.</param>
    public OutboxDomainEventWorker(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        IEnumerable<ActivitySource> activitySources = null,
        OutboxDomainEventOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory?.CreateLogger<OutboxDomainEventWorker<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxDomainEventWorker<TContext>>();
        this.serviceProvider = serviceProvider;
        this.options = options ?? new OutboxDomainEventOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
        this.options.ProcessingCount = this.options.ProcessingCount > 0 ? this.options.ProcessingCount : int.MaxValue;
        this.options.LeaseDuration = this.options.LeaseDuration > TimeSpan.Zero ? this.options.LeaseDuration : TimeSpan.FromSeconds(30);
        this.options.LeaseRenewalInterval = this.options.LeaseRenewalInterval < TimeSpan.Zero ? TimeSpan.Zero : this.options.LeaseRenewalInterval;
        this.activitySources = activitySources ?? [];
        this.contextTypeName = typeof(TContext).Name;
    }

    /// <summary>
    /// Processes retryable domain events from persistence.
    /// </summary>
    /// <param name="eventId">The optional event identifier to process immediately.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <example>
    /// <code>
    /// await worker.ProcessAsync(cancellationToken: cancellationToken);
    /// await worker.ProcessAsync(eventId: "4f44d65d2e134ee7b0fc7b8adce59dc6", cancellationToken: cancellationToken);
    /// </code>
    /// </example>
    public async Task ProcessAsync(string eventId = null, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProcessing(this.logger, Constants.LogKey, this.contextTypeName, eventId);

        var candidateIds = await this.GetCandidateEventIdsAsync(eventId, cancellationToken).AnyContext();
        var count = 0;

        foreach (var outboxEventId in candidateIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (await this.ProcessStoredEventAsync(outboxEventId, cancellationToken).AnyContext())
            {
                count++;
            }
        }

        TypedLogger.LogProcessed(this.logger, Constants.LogKey, this.contextTypeName, count);
    }

    /// <summary>
    /// Purges persisted domain events from the outbox table.
    /// </summary>
    /// <param name="processedOnly">If set to <c>true</c>, only processed domain events are removed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <example>
    /// <code>
    /// await worker.PurgeAsync(processedOnly: true, cancellationToken);
    /// </code>
    /// </example>
    public async Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        TypedLogger.LogPurging(this.logger, "DOM", this.contextTypeName);

        try
        {
            if (processedOnly)
            {
                await context.OutboxDomainEvents
                    .Where(e => e.ProcessedDate != null)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            else
            {
                await context.OutboxDomainEvents
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }
        catch (SqlException ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox domain event purge error: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
    }

    /// <summary>
    /// Archives processed domain events that are older than the configured archive threshold.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <example>
    /// <code>
    /// await worker.ArchiveAsync(cancellationToken);
    /// </code>
    /// </example>
    public async Task ArchiveAsync(CancellationToken cancellationToken = default)
    {
        await this.ArchiveProcessedEventsAsync(cancellationToken).AnyContext();
    }

    private async Task<IReadOnlyList<Guid>> GetCandidateEventIdsAsync(string eventId, CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = DateTimeOffset.UtcNow;

        var query = context.OutboxDomainEvents
            .AsNoTracking()
            .Where(outboxEvent => !outboxEvent.IsArchived)
            .Where(outboxEvent => outboxEvent.ProcessedDate == null)
            .Where(outboxEvent => !string.IsNullOrEmpty(outboxEvent.Type));

        if (!string.IsNullOrEmpty(eventId))
        {
            query = context.OutboxDomainEvents
            .AsNoTracking()
            .Where(outboxEvent => !outboxEvent.IsArchived)
            .Where(outboxEvent => outboxEvent.ProcessedDate == null)
            .Where(outboxEvent => !string.IsNullOrEmpty(outboxEvent.Type)).Where(outboxEvent => outboxEvent.EventId == eventId);
        }

        if (this.IsSqlite(context))
        {
            var candidates = await context.OutboxDomainEvents
            .AsNoTracking()
            .Where(outboxEvent => !outboxEvent.IsArchived)
            .Where(outboxEvent => outboxEvent.ProcessedDate == null)
            .Where(outboxEvent => !string.IsNullOrEmpty(outboxEvent.Type))
                .OrderBy(outboxEvent => outboxEvent.CreatedDate)
                .Take(this.options.ProcessingCount)
                .ToListAsync(cancellationToken)
                .AnyContext();

            return candidates
                .Where(outboxEvent => this.CanClaimEvent(outboxEvent, now))
                .OrderBy(outboxEvent => outboxEvent.CreatedDate)
                .Select(outboxEvent => outboxEvent.Id)
                .ToList();
        }

        return await context.OutboxDomainEvents
            .AsNoTracking()
            .Where(outboxEvent => !outboxEvent.IsArchived)
            .Where(outboxEvent => outboxEvent.ProcessedDate == null)
            .Where(outboxEvent => !string.IsNullOrEmpty(outboxEvent.Type))
            .Where(outboxEvent => outboxEvent.LockedUntil == null || outboxEvent.LockedUntil < now)
            .OrderBy(outboxEvent => outboxEvent.CreatedDate)
            .Take(this.options.ProcessingCount)
            .Select(outboxEvent => outboxEvent.Id)
            .ToListAsync(cancellationToken)
            .AnyContext();
    }

    private async Task<bool> ProcessStoredEventAsync(Guid outboxEventId, CancellationToken cancellationToken)
    {
        var claimedEvent = await this.TryClaimEventAsync(outboxEventId, cancellationToken).AnyContext();
        if (claimedEvent is null)
        {
            return false;
        }

        using var renewalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewalTask = this.StartLeaseRenewalAsync(outboxEventId, renewalCts.Token);

        try
        {
            await this.ProcessClaimedEventAsync(claimedEvent, cancellationToken).AnyContext();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown should release the lease without marking the event as failed.
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox domain event processing crashed after lease acquisition (eventId={DomainEventId}, lockedBy={LockedBy})", Constants.LogKey, claimedEvent.EventId, this.instanceName);
            this.MarkProcessingFailure(claimedEvent, ex, this.GetAttempts(claimedEvent) + 1);
        }
        finally
        {
            renewalCts.Cancel();
        }

        var leaseMaintained = await renewalTask.AnyContext();
        if (!leaseMaintained)
        {
            this.logger.LogWarning("{LogKey} outbox domain event lease was lost while processing, final state will not be persisted (eventId={DomainEventId}, lockedBy={LockedBy})", Constants.LogKey, claimedEvent.EventId, this.instanceName);
            return false;
        }

        await this.FinalizeClaimedEventAsync(claimedEvent, cancellationToken).AnyContext();
        return true;
    }

    private async Task<OutboxDomainEvent> TryClaimEventAsync(Guid outboxEventId, CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = DateTimeOffset.UtcNow;
        var lockedUntil = now.Add(this.options.LeaseDuration);
        var concurrencyVersion = Guid.NewGuid();

        if (this.SupportsExecuteUpdate(context))
        {
            var claimed = await context.OutboxDomainEvents
                .Where(outboxEvent =>
                    outboxEvent.Id == outboxEventId &&
                    !outboxEvent.IsArchived &&
                    outboxEvent.ProcessedDate == null &&
                    (outboxEvent.LockedUntil == null || outboxEvent.LockedUntil < now))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(outboxEvent => outboxEvent.LockedBy, this.instanceName)
                        .SetProperty(outboxEvent => outboxEvent.LockedUntil, lockedUntil)
                        .SetProperty(outboxEvent => outboxEvent.ProcessingStartedDate, now)
                        .SetProperty(outboxEvent => outboxEvent.ConcurrencyVersion, concurrencyVersion),
                    cancellationToken)
                .AnyContext();

            if (claimed == 0)
            {
                this.logger.LogDebug("{LogKey} outbox domain event lease claim skipped because another worker already acquired or changed the row (outboxEventId={OutboxEventId}, lockedBy={LockedBy})", Constants.LogKey, outboxEventId, this.instanceName);
                return null;
            }

            return await context.OutboxDomainEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(outboxEvent => outboxEvent.Id == outboxEventId, cancellationToken)
                .AnyContext();
        }

        var outboxEvent = await context.OutboxDomainEvents
            .FirstOrDefaultAsync(item => item.Id == outboxEventId, cancellationToken)
            .AnyContext();

        if (!this.CanClaimEvent(outboxEvent, now))
        {
            this.logger.LogDebug("{LogKey} outbox domain event lease claim skipped because another worker already acquired or changed the row (outboxEventId={OutboxEventId}, lockedBy={LockedBy})", Constants.LogKey, outboxEventId, this.instanceName);
            return null;
        }

        outboxEvent.LockedBy = this.instanceName;
        outboxEvent.LockedUntil = lockedUntil;
        outboxEvent.ProcessingStartedDate = now;
        outboxEvent.ConcurrencyVersion = concurrencyVersion;

        try
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
            return outboxEvent;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            this.logger.LogDebug(ex, "{LogKey} outbox domain event lease claim lost due to optimistic concurrency (outboxEventId={OutboxEventId}, lockedBy={LockedBy})", Constants.LogKey, outboxEventId, this.instanceName);
            return null;
        }
    }

    private Task<bool> StartLeaseRenewalAsync(Guid outboxEventId, CancellationToken cancellationToken)
    {
        var renewalInterval = this.GetLeaseRenewalInterval();
        if (renewalInterval <= TimeSpan.Zero)
        {
            return Task.FromResult(true);
        }

        return this.RenewLeaseAsync(outboxEventId, renewalInterval, cancellationToken);
    }

    private async Task<bool> RenewLeaseAsync(Guid outboxEventId, TimeSpan renewalInterval, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(renewalInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var renewed = await this.TryRenewLeaseAsync(outboxEventId, cancellationToken).AnyContext();
                if (!renewed)
                {
                    return false;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return true;
        }

        return true;
    }

    private TimeSpan GetLeaseRenewalInterval()
    {
        if (this.options.LeaseRenewalInterval <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return this.options.LeaseRenewalInterval < this.options.LeaseDuration
            ? this.options.LeaseRenewalInterval
            : TimeSpan.FromTicks(Math.Max(1, this.options.LeaseDuration.Ticks / 2));
    }

    private async Task<bool> TryRenewLeaseAsync(Guid outboxEventId, CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var lockedUntil = DateTimeOffset.UtcNow.Add(this.options.LeaseDuration);
        var concurrencyVersion = Guid.NewGuid();

        if (this.SupportsExecuteUpdate(context))
        {
            var renewed = await context.OutboxDomainEvents
                .Where(outboxEvent => outboxEvent.Id == outboxEventId && outboxEvent.LockedBy == this.instanceName)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(outboxEvent => outboxEvent.LockedUntil, lockedUntil)
                        .SetProperty(outboxEvent => outboxEvent.ConcurrencyVersion, concurrencyVersion),
                    cancellationToken)
                .AnyContext();

            if (renewed == 0)
            {
                this.logger.LogWarning("{LogKey} outbox domain event lease renewal skipped because ownership was lost (outboxEventId={OutboxEventId}, lockedBy={LockedBy})", Constants.LogKey, outboxEventId, this.instanceName);
                return false;
            }

            return renewed > 0;
        }

        var outboxEvent = await context.OutboxDomainEvents
            .FirstOrDefaultAsync(item => item.Id == outboxEventId, cancellationToken)
            .AnyContext();

        if (outboxEvent is null || !string.Equals(outboxEvent.LockedBy, this.instanceName, StringComparison.Ordinal))
        {
            this.logger.LogWarning("{LogKey} outbox domain event lease renewal skipped because ownership was lost (outboxEventId={OutboxEventId}, lockedBy={LockedBy})", Constants.LogKey, outboxEventId, this.instanceName);
            return false;
        }

        outboxEvent.LockedUntil = lockedUntil;
        outboxEvent.ConcurrencyVersion = concurrencyVersion;

        try
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            this.logger.LogWarning(ex, "{LogKey} outbox domain event lease renewal lost due to optimistic concurrency (outboxEventId={OutboxEventId}, lockedBy={LockedBy})", Constants.LogKey, outboxEventId, this.instanceName);
            return false;
        }
    }

    private async Task ProcessClaimedEventAsync(OutboxDomainEvent outboxEvent, CancellationToken cancellationToken)
    {
        var attempts = this.GetAttempts(outboxEvent) + 1;
        if (attempts > this.options.RetryCount)
        {
            this.MarkMaxAttemptsReached(outboxEvent, attempts);
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(outboxEvent.Type))
        {
            this.MarkUnresolvableType(outboxEvent, attempts, "(empty)");
            return;
        }

        var type = Type.GetType(outboxEvent.Type);
        if (type is null)
        {
            TypedLogger.LogEventTypeNotResolved(this.logger, "DOM", outboxEvent.EventId, outboxEvent.Type.Split(',')[0]);
            this.MarkUnresolvableType(outboxEvent, attempts, outboxEvent.Type.Split(',')[0]);
            return;
        }

        if (this.options.Serializer.Deserialize(outboxEvent.Content, type) is not IDomainEvent @event)
        {
            this.MarkProcessingFailure(outboxEvent, new InvalidOperationException($"domain event payload could not be deserialized for type '{type.PrettyName(false)}'"), attempts);
            return;
        }

        var eventType = type.PrettyName(false);
        var correlationId = outboxEvent.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString();
        var flowId = outboxEvent.Properties?.GetValue(Constants.FlowIdKey)?.ToString();
        var moduleName = outboxEvent.Properties?.GetValue(ModuleConstants.ModuleNameKey)?.ToString();
        var parentId = outboxEvent.Properties?.GetValue(ModuleConstants.ActivityParentIdKey)?.ToString();

        using var scope = this.serviceProvider.CreateScope();
        var publisher = new NotifierDomainEventPublisher(this.loggerFactory, scope.ServiceProvider.GetRequiredService<INotifier>());

        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [ModuleConstants.ModuleNameKey] = moduleName,
            [Constants.CorrelationIdKey] = correlationId,
            [Constants.FlowIdKey] = flowId
        }))
        {
            await this.activitySources.Find(moduleName)
                .StartActvity(
                    $"OUTBOX_PROCESS {eventType}",
                    async (_, activityCancellationToken) =>
                    {
                        await publisher.Send(@event, activityCancellationToken).AnyContext();
                        this.MarkProcessingSucceeded(outboxEvent, attempts);
                    },
                    ActivityKind.Consumer,
                    parentId,
                    new Dictionary<string, string>
                    {
                        ["domain.event_id"] = @event.EventId.ToString(),
                        ["domain.event_type"] = eventType
                    },
                    new Dictionary<string, string>
                    {
                        [ActivityConstants.ModuleNameTagKey] = moduleName,
                        [ActivityConstants.CorrelationIdTagKey] = correlationId,
                        [ActivityConstants.FlowIdTagKey] = flowId
                    },
                    cancellationToken: cancellationToken);
        }
    }

    private async Task FinalizeClaimedEventAsync(OutboxDomainEvent outboxEvent, CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var storedEvent = await context.OutboxDomainEvents
            .FirstOrDefaultAsync(item => item.Id == outboxEvent.Id, cancellationToken)
            .AnyContext();

        if (storedEvent is null)
        {
            return;
        }

        if (!string.Equals(storedEvent.LockedBy, this.instanceName, StringComparison.Ordinal))
        {
            this.logger.LogWarning("{LogKey} outbox domain event lease ownership changed before finalize (eventId={DomainEventId}, lockedBy={LockedBy}, currentLockedBy={CurrentLockedBy})", Constants.LogKey, outboxEvent.EventId, this.instanceName, storedEvent.LockedBy);
            return;
        }

        this.ApplyProcessedState(storedEvent, outboxEvent);
        await this.FinalizeClaimedEventAsync(context, storedEvent, cancellationToken).AnyContext();
    }

    private async Task FinalizeClaimedEventAsync(TContext context, OutboxDomainEvent outboxEvent, CancellationToken cancellationToken)
    {
        outboxEvent.LockedBy = null;
        outboxEvent.LockedUntil = null;

        outboxEvent.AdvanceConcurrencyVersion();

        try
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            this.logger.LogWarning(ex, "{LogKey} outbox domain event finalize lost due to optimistic concurrency (eventId={DomainEventId}, lockedBy={LockedBy})", Constants.LogKey, outboxEvent.EventId, this.instanceName);
        }
    }

    private void ApplyProcessedState(OutboxDomainEvent target, OutboxDomainEvent source)
    {
        target.ProcessedDate = source.ProcessedDate;
        target.ProcessingStartedDate = source.ProcessingStartedDate;
        target.LastError = source.LastError;
        target.IsArchived = source.IsArchived;
        target.ArchivedDate = source.ArchivedDate;
        target.Properties = source.Properties?.ToDictionary(item => item.Key, item => item.Value) ?? [];
    }

    private void MarkMaxAttemptsReached(OutboxDomainEvent outboxEvent, int attempts)
    {
        this.logger.LogWarning("{LogKey} outbox domain event processing skipped: max attempts reached (eventId={DomainEventId}, eventType={DomainEventType}, attempts={DomainEventAttempts})", Constants.LogKey, outboxEvent.EventId, outboxEvent.Type?.Split(',')[0], attempts - 1);

        var existingMessage = outboxEvent.Properties?.GetValue(OutboxDomainEventPropertyConstants.ProcessMessageKey)?.ToString();
        outboxEvent.ProcessedDate ??= DateTimeOffset.UtcNow;
        outboxEvent.LastError = this.TruncateError($"max attempts reached (eventId={outboxEvent.EventId}, eventType={outboxEvent.Type?.Split(',')[0]}, attempts={attempts - 1}) {existingMessage}".Trim());
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessStatusKey, "Failure");
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessMessageKey, outboxEvent.LastError);
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessAttemptsKey, attempts - 1);
    }

    private void MarkUnresolvableType(OutboxDomainEvent outboxEvent, int attempts, string eventType)
    {
        outboxEvent.ProcessedDate ??= DateTimeOffset.UtcNow;
        outboxEvent.LastError = this.TruncateError($"event type could not be resolved (eventId={outboxEvent.EventId}, eventType={eventType})");
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessStatusKey, "Failure");
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessMessageKey, outboxEvent.LastError);
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessAttemptsKey, attempts);
    }

    private void MarkProcessingSucceeded(OutboxDomainEvent outboxEvent, int attempts)
    {
        outboxEvent.ProcessedDate ??= DateTimeOffset.UtcNow;
        outboxEvent.LastError = null;
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessStatusKey, "Success");
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessMessageKey, string.Empty);
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessAttemptsKey, attempts);
    }

    private void MarkProcessingFailure(OutboxDomainEvent outboxEvent, Exception exception, int attempts)
    {
        this.logger.LogError(exception, "{LogKey} outbox domain event processing failed: {ErrorMessage} (eventId={DomainEventId}, eventType={DomainEventType}, attempts={DomainEventAttempts})", Constants.LogKey, exception.Message, outboxEvent.EventId, outboxEvent.Type?.Split(',')[0], attempts);

        outboxEvent.LastError = this.TruncateError($"[{exception.GetType().Name}] {exception.Message}");
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessStatusKey, "Failure");
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessMessageKey, outboxEvent.LastError);
        outboxEvent.Properties.AddOrUpdate(OutboxDomainEventPropertyConstants.ProcessAttemptsKey, attempts);
    }

    private int GetAttempts(OutboxDomainEvent outboxEvent)
    {
        return outboxEvent.Properties?.GetValue(OutboxDomainEventPropertyConstants.ProcessAttemptsKey)?.ToString().To<int>() ?? 0;
    }

    private string TruncateError(string value)
    {
        if (value.IsNullOrEmpty())
        {
            return value;
        }

        return value.Length <= 4000 ? value : value[..4000];
    }

    private bool SupportsExecuteUpdate(TContext dbContext)
    {
        var providerName = dbContext.Database.ProviderName;

        return !(providerName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) ?? false) &&
            !(providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private bool IsSqlite(TContext dbContext)
    {
        return dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private bool CanClaimEvent(OutboxDomainEvent outboxEvent, DateTimeOffset now)
    {
        return outboxEvent is not null &&
            !outboxEvent.IsArchived &&
            outboxEvent.ProcessedDate == null &&
            (outboxEvent.LockedUntil == null || outboxEvent.LockedUntil < now);
    }

    private bool ShouldArchive(OutboxDomainEvent outboxEvent)
    {
        return this.options.AutoArchiveAfter.HasValue &&
            outboxEvent.ProcessedDate.HasValue &&
            outboxEvent.ProcessedDate.Value.Add(this.options.AutoArchiveAfter.Value) <= DateTimeOffset.UtcNow;
    }

    private async Task ArchiveProcessedEventsAsync(CancellationToken cancellationToken)
    {
        if (!this.options.AutoArchiveAfter.HasValue)
        {
            return;
        }

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var archiveThreshold = DateTimeOffset.UtcNow.Subtract(this.options.AutoArchiveAfter.Value);

        if (this.SupportsExecuteUpdate(context))
        {
            var archivedDate = DateTimeOffset.UtcNow;
            var concurrencyVersion = Guid.NewGuid();

            await context.OutboxDomainEvents
                .Where(outboxEvent =>
                    !outboxEvent.IsArchived &&
                    outboxEvent.ProcessedDate != null &&
                    outboxEvent.ProcessedDate <= archiveThreshold)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(outboxEvent => outboxEvent.IsArchived, true)
                        .SetProperty(outboxEvent => outboxEvent.ArchivedDate, archivedDate)
                        .SetProperty(outboxEvent => outboxEvent.ConcurrencyVersion, concurrencyVersion),
                    cancellationToken)
                .AnyContext();

            return;
        }

        var outboxEvents = await context.OutboxDomainEvents
            .Where(outboxEvent =>
                !outboxEvent.IsArchived &&
                outboxEvent.ProcessedDate != null &&
                outboxEvent.ProcessedDate <= archiveThreshold)
            .ToListAsync(cancellationToken)
            .AnyContext();

        if (outboxEvents.Count == 0)
        {
            return;
        }

        var archivedAt = DateTimeOffset.UtcNow;
        foreach (var outboxEvent in outboxEvents)
        {
            outboxEvent.IsArchived = true;
            outboxEvent.ArchivedDate = archivedAt;
            outboxEvent.AdvanceConcurrencyVersion();
        }

        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} outbox domain events processing (context={DbContextType}, eventId={DomainEventId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string dbContextType, string domainEventId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} outbox domain events processed (context={DbContextType}, count={outboxDomainEventProcessedCount})")]
        public static partial void LogProcessed(ILogger logger, string logKey, string dbContextType, int outboxDomainEventProcessedCount);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} outbox domain events purging (context={DbContextType})")]
        public static partial void LogPurging(ILogger logger, string logKey, string dbContextType);

        [LoggerMessage(3, LogLevel.Error, "{LogKey} outbox domain event type could not be resolved (eventId={DomainEventId}, eventType={DomainEventType})")]
        public static partial void LogEventTypeNotResolved(ILogger logger, string logKey, string domainEventId, string domainEventType);
    }
}
