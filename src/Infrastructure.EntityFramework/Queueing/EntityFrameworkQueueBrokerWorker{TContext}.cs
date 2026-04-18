namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Processes persisted queue messages for the Entity Framework transport.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IQueueingContext"/>.</typeparam>
public class EntityFrameworkQueueBrokerWorker<TContext>
    where TContext : DbContext, IQueueingContext
{
    private readonly ILogger<EntityFrameworkQueueBrokerWorker<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly TContext context;
    private readonly Func<TContext> contextFactory;
    private readonly EntityFrameworkQueueBroker<TContext> broker;
    private readonly EntityFrameworkQueueBrokerOptions options;
    private readonly string instanceName = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    /// <summary>
    /// Initializes a new worker instance that resolves its context from the service provider.
    /// </summary>
    public EntityFrameworkQueueBrokerWorker(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        EntityFrameworkQueueBroker<TContext> broker,
        EntityFrameworkQueueBrokerOptions options)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));
        EnsureArg.IsNotNull(broker, nameof(broker));

        this.logger = loggerFactory?.CreateLogger<EntityFrameworkQueueBrokerWorker<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<EntityFrameworkQueueBrokerWorker<TContext>>();
        this.serviceProvider = serviceProvider;
        this.broker = broker;
        this.options = options ?? new EntityFrameworkQueueBrokerOptions();
    }

    /// <summary>
    /// Initializes a new worker instance that processes messages against a provided context.
    /// </summary>
    public EntityFrameworkQueueBrokerWorker(
        ILoggerFactory loggerFactory,
        TContext context,
        EntityFrameworkQueueBroker<TContext> broker,
        EntityFrameworkQueueBrokerOptions options)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(broker, nameof(broker));

        this.logger = loggerFactory?.CreateLogger<EntityFrameworkQueueBrokerWorker<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<EntityFrameworkQueueBrokerWorker<TContext>>();
        this.context = context;
        this.contextFactory = CreateContextFactory(context);
        this.broker = broker;
        this.options = options ?? new EntityFrameworkQueueBrokerOptions();
    }

    /// <summary>
    /// Processes retryable queue messages from persistence.
    /// </summary>
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var candidateIds = await this.GetCandidateMessageIdsAsync(cancellationToken);

        foreach (var messageId in candidateIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await this.ProcessMessageAsync(messageId, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<Guid>> GetCandidateMessageIdsAsync(CancellationToken cancellationToken)
    {
        using var lease = this.CreateContextLease(out var context);
        var now = DateTimeOffset.UtcNow;

        if (this.IsSqlite(context))
        {
            var candidates = await context.QueueMessages
                .AsNoTracking()
                .Where(message => !message.IsArchived)
                .Where(message =>
                    message.Status == QueueMessageStatus.Pending ||
                    message.Status == QueueMessageStatus.Failed ||
                    message.Status == QueueMessageStatus.WaitingForHandler ||
                    message.Status == QueueMessageStatus.Processing)
                .ToListAsync(cancellationToken)
                .AnyContext();

            return candidates
                .Where(message => this.CanClaimMessage(message, now))
                .OrderBy(message => message.CreatedDate)
                .Take(this.options.ProcessingCount)
                .Select(message => message.Id)
                .ToList();
        }

        return await context.QueueMessages
            .AsNoTracking()
            .Where(message => !message.IsArchived)
            .Where(message =>
                message.Status == QueueMessageStatus.Pending ||
                message.Status == QueueMessageStatus.Failed ||
                message.Status == QueueMessageStatus.WaitingForHandler ||
                (message.Status == QueueMessageStatus.Processing && message.LockedUntil < now))
            .Where(message => message.LockedUntil == null || message.LockedUntil < now)
            .OrderBy(message => message.CreatedDate)
            .Take(this.options.ProcessingCount)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken)
            .AnyContext();
    }

    private async Task ProcessMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var claimedMessage = await this.TryClaimMessageAsync(messageId, cancellationToken);
        if (claimedMessage is null)
        {
            return;
        }

        using var renewalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewalTask = this.StartLeaseRenewalAsync(messageId, renewalCts.Token);

        try
        {
            await this.broker.ProcessStoredMessageAsync(claimedMessage, cancellationToken);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(ex, "{LogKey} queue message processing crashed after lease acquisition (queueMessageId={QueueMessageId}, lockedBy={LockedBy})", Application.Queueing.Constants.LogKey, messageId, this.instanceName);
            this.MarkMessageFailed(claimedMessage, ex.Message);
        }
        finally
        {
            renewalCts.Cancel();
        }

        var leaseMaintained = await renewalTask.AnyContext();
        if (!leaseMaintained)
        {
            this.logger.LogWarning("{LogKey} queue message lease was lost while processing, final state will not be persisted (queueMessageId={QueueMessageId}, lockedBy={LockedBy})", Application.Queueing.Constants.LogKey, messageId, this.instanceName);
            return;
        }

        await this.FinalizeClaimedMessageAsync(claimedMessage, cancellationToken);
    }

    private async Task<QueueMessage> TryClaimMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        using var lease = this.CreateContextLease(out var context);
        var now = DateTimeOffset.UtcNow;
        var lockedUntil = now.Add(this.options.LeaseDuration);
        var concurrencyVersion = Guid.NewGuid();

        if (this.SupportsExecuteUpdate(context))
        {
            var claimed = await context.QueueMessages
                .Where(message =>
                    !message.IsArchived &&
                    message.Id == messageId &&
                    (message.Status == QueueMessageStatus.Pending ||
                     message.Status == QueueMessageStatus.Failed ||
                     message.Status == QueueMessageStatus.WaitingForHandler ||
                     (message.Status == QueueMessageStatus.Processing && message.LockedUntil < now)) &&
                    (message.LockedUntil == null || message.LockedUntil < now))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(message => message.LockedBy, this.instanceName)
                        .SetProperty(message => message.LockedUntil, lockedUntil)
                        .SetProperty(message => message.Status, QueueMessageStatus.Processing)
                        .SetProperty(message => message.ProcessingStartedDate, now)
                        .SetProperty(message => message.ConcurrencyVersion, concurrencyVersion),
                    cancellationToken)
                .AnyContext();

            if (claimed == 0)
            {
                this.logger.LogDebug("{LogKey} queue message lease claim skipped because another worker already acquired or changed the row (queueMessageId={QueueMessageId}, lockedBy={LockedBy})", Application.Queueing.Constants.LogKey, messageId, this.instanceName);
                return null;
            }

            return await context.QueueMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken)
                .AnyContext();
        }

        var message = await context.QueueMessages
            .FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken)
            .AnyContext();

        if (!this.CanClaimMessage(message, now))
        {
            this.logger.LogDebug("{LogKey} queue message lease claim skipped because another worker already acquired or changed the row (queueMessageId={QueueMessageId}, lockedBy={LockedBy})", Application.Queueing.Constants.LogKey, messageId, this.instanceName);
            return null;
        }

        message.LockedBy = this.instanceName;
        message.LockedUntil = lockedUntil;
        message.Status = QueueMessageStatus.Processing;
        message.ProcessingStartedDate = now;
        message.ConcurrencyVersion = concurrencyVersion;

        try
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
            return message;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            this.logger.LogDebug(ex, "{LogKey} queue message lease claim lost due to optimistic concurrency (queueMessageId={QueueMessageId}, lockedBy={LockedBy})", Application.Queueing.Constants.LogKey, messageId, this.instanceName);
            return null;
        }
    }

    private Task<bool> StartLeaseRenewalAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var renewalInterval = this.GetLeaseRenewalInterval();
        if (renewalInterval <= TimeSpan.Zero)
        {
            return Task.FromResult(true);
        }

        return this.RenewLeaseAsync(messageId, renewalInterval, cancellationToken);
    }

    private async Task<bool> RenewLeaseAsync(Guid messageId, TimeSpan renewalInterval, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(renewalInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var renewed = await this.TryRenewLeaseAsync(messageId, cancellationToken);
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

    private async Task<bool> TryRenewLeaseAsync(Guid messageId, CancellationToken cancellationToken)
    {
        using var lease = this.CreateContextLease(out var context);
        var lockedUntil = DateTimeOffset.UtcNow.Add(this.options.LeaseDuration);
        var concurrencyVersion = Guid.NewGuid();

        if (this.SupportsExecuteUpdate(context))
        {
            var renewed = await context.QueueMessages
                .Where(message => message.Id == messageId && message.LockedBy == this.instanceName)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(message => message.LockedUntil, lockedUntil)
                        .SetProperty(message => message.ConcurrencyVersion, concurrencyVersion),
                    cancellationToken)
                .AnyContext();

            if (renewed == 0)
            {
                this.logger.LogWarning("{LogKey} queue message lease renewal skipped because ownership was lost (queueMessageId={QueueMessageId}, lockedBy={LockedBy})", Application.Queueing.Constants.LogKey, messageId, this.instanceName);
                return false;
            }

            return renewed > 0;
        }

        var message = await context.QueueMessages.FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken).AnyContext();
        if (message is null || !string.Equals(message.LockedBy, this.instanceName, StringComparison.Ordinal))
        {
            this.logger.LogWarning("{LogKey} queue message lease renewal skipped because ownership was lost (queueMessageId={QueueMessageId}, lockedBy={LockedBy})", Application.Queueing.Constants.LogKey, messageId, this.instanceName);
            return false;
        }

        message.LockedUntil = lockedUntil;
        message.ConcurrencyVersion = concurrencyVersion;

        try
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            this.logger.LogWarning(ex, "{LogKey} queue message lease renewal lost due to optimistic concurrency (queueMessageId={QueueMessageId}, lockedBy={LockedBy})", Application.Queueing.Constants.LogKey, messageId, this.instanceName);
            return false;
        }
    }

    private async Task FinalizeClaimedMessageAsync(QueueMessage message, CancellationToken cancellationToken)
    {
        using var lease = this.CreateContextLease(out var context);
        var storedMessage = await context.QueueMessages.FirstOrDefaultAsync(item => item.Id == message.Id, cancellationToken).AnyContext();
        if (storedMessage is null)
        {
            return;
        }

        if (!string.Equals(storedMessage.LockedBy, this.instanceName, StringComparison.Ordinal))
        {
            this.logger.LogWarning("{LogKey} queue message lease ownership changed before finalize (queueMessageId={QueueMessageId}, lockedBy={LockedBy}, currentLockedBy={CurrentLockedBy})", Application.Queueing.Constants.LogKey, message.Id, this.instanceName, storedMessage.LockedBy);
            return;
        }

        this.ApplyProcessedState(storedMessage, message);
        await this.FinalizeClaimedMessageAsync(context, storedMessage, cancellationToken);
    }

    private async Task FinalizeClaimedMessageAsync(TContext context, QueueMessage message, CancellationToken cancellationToken)
    {
        message.LockedBy = null;
        message.LockedUntil = null;

        if (this.ShouldArchive(message))
        {
            message.IsArchived = true;
            message.ArchivedDate = DateTimeOffset.UtcNow;
        }

        message.AdvanceConcurrencyVersion();

        try
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            this.logger.LogWarning(ex, "{LogKey} queue message finalize lost due to optimistic concurrency (queueMessageId={QueueMessageId}, lockedBy={LockedBy})", Application.Queueing.Constants.LogKey, message.Id, this.instanceName);
        }
    }

    private void ApplyProcessedState(QueueMessage target, QueueMessage source)
    {
        target.Status = source.Status;
        target.AttemptCount = source.AttemptCount;
        target.RegisteredHandlerType = source.RegisteredHandlerType;
        target.LastError = source.LastError;
        target.ProcessedDate = source.ProcessedDate;
        target.IsArchived = source.IsArchived;
        target.ArchivedDate = source.ArchivedDate;
    }

    private void MarkMessageFailed(QueueMessage message, string error)
    {
        message.Status = message.AttemptCount >= this.options.MaxDeliveryAttempts
            ? QueueMessageStatus.DeadLettered
            : QueueMessageStatus.Failed;
        message.LastError = error;
        message.ProcessedDate = message.Status == QueueMessageStatus.DeadLettered ? DateTimeOffset.UtcNow : null;
    }

    private bool ShouldArchive(QueueMessage message)
    {
        if (!this.options.AutoArchiveAfter.HasValue || !message.ProcessedDate.HasValue)
        {
            return false;
        }

        return this.options.AutoArchiveStatuses.SafeAny(status => status == message.Status) &&
            message.ProcessedDate.Value.Add(this.options.AutoArchiveAfter.Value) <= DateTimeOffset.UtcNow;
    }

    private IDisposable CreateContextLease(out TContext dbContext)
    {
        if (this.serviceProvider is not null)
        {
            var scope = this.serviceProvider.CreateScope();
            dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            return scope;
        }

        if (this.contextFactory is not null)
        {
            dbContext = this.contextFactory();
            return dbContext;
        }

        dbContext = this.context;
        return NoopDisposable.Instance;
    }

    private static Func<TContext> CreateContextFactory(TContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        var options = context.GetService<IDbContextOptions>() as DbContextOptions;

        return () =>
        {
            try
            {
                return Activator.CreateInstance(typeof(TContext), options) as TContext
                    ?? throw new ArgumentException($"Unable to instantiate DbContext of type {typeof(TContext).Name}. Ensure it has a constructor accepting DbContextOptions<{typeof(TContext).Name}>.");
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to create instance of DbContext type {typeof(TContext).Name}.", ex);
            }
        };
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

    private bool CanClaimMessage(QueueMessage message, DateTimeOffset now)
    {
        return message is not null &&
               !message.IsArchived &&
               (message.Status == QueueMessageStatus.Pending ||
                message.Status == QueueMessageStatus.Failed ||
                message.Status == QueueMessageStatus.WaitingForHandler ||
                (message.Status == QueueMessageStatus.Processing && message.LockedUntil < now)) &&
               (message.LockedUntil == null || message.LockedUntil < now);
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
