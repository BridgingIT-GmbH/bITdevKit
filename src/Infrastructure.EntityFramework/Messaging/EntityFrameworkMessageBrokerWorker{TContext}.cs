// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Processes persisted broker messages for the Entity Framework transport.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IMessagingContext"/>.</typeparam>
public class EntityFrameworkMessageBrokerWorker<TContext>
    where TContext : DbContext, IMessagingContext
{
    private readonly ILogger<EntityFrameworkMessageBrokerWorker<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly TContext context;
    private readonly Func<TContext> contextFactory;
    private readonly EntityFrameworkMessageBroker<TContext> broker;
    private readonly EntityFrameworkMessageBrokerOptions options;
    private readonly string instanceName = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    /// <summary>
    /// Initializes a new worker instance that resolves its context from the service provider.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="serviceProvider">The service provider used to create scoped database contexts.</param>
    /// <param name="broker">The broker used to process stored broker rows.</param>
    /// <param name="options">The broker runtime options.</param>
    public EntityFrameworkMessageBrokerWorker(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        EntityFrameworkMessageBroker<TContext> broker,
        EntityFrameworkMessageBrokerOptions options)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));
        EnsureArg.IsNotNull(broker, nameof(broker));

        this.logger = loggerFactory?.CreateLogger<EntityFrameworkMessageBrokerWorker<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<EntityFrameworkMessageBrokerWorker<TContext>>();
        this.serviceProvider = serviceProvider;
        this.broker = broker;
        this.options = options ?? new EntityFrameworkMessageBrokerOptions();
    }

    /// <summary>
    /// Initializes a new worker instance that processes messages against a provided context.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="context">The database context used for broker persistence.</param>
    /// <param name="broker">The broker used to process stored broker rows.</param>
    /// <param name="options">The broker runtime options.</param>
    public EntityFrameworkMessageBrokerWorker(
        ILoggerFactory loggerFactory,
        TContext context,
        EntityFrameworkMessageBroker<TContext> broker,
        EntityFrameworkMessageBrokerOptions options)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(broker, nameof(broker));

        this.logger = loggerFactory?.CreateLogger<EntityFrameworkMessageBrokerWorker<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<EntityFrameworkMessageBrokerWorker<TContext>>();
        this.context = context;
        this.contextFactory = CreateContextFactory(context);
        this.broker = broker;
        this.options = options ?? new EntityFrameworkMessageBrokerOptions();
    }

    /// <summary>
    /// Processes retryable broker messages from persistence.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the current processing cycle is done.</returns>
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var candidateIds = await this.GetCandidateMessageIdsAsync(cancellationToken);

        foreach (var brokerMessageId in candidateIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await this.ProcessMessageAsync(brokerMessageId, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<Guid>> GetCandidateMessageIdsAsync(CancellationToken cancellationToken)
    {
        using var lease = this.CreateContextLease(out var context);

        return await this.QueryCandidateMessageIdsAsync(context, cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> QueryCandidateMessageIdsAsync(TContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        if (this.IsSqlite(context))
        {
            var candidates = await context.BrokerMessages
                .AsNoTracking()
                .Where(message => !message.IsArchived)
                .Where(message =>
                    message.Status == BrokerMessageStatus.Pending ||
                    message.Status == BrokerMessageStatus.Failed ||
                    message.Status == BrokerMessageStatus.Processing)
                .ToListAsync(cancellationToken)
                .AnyContext();

            return candidates
                .Where(message => this.CanClaimMessage(message, now))
                .OrderBy(message => message.CreatedDate)
                .Take(this.options.ProcessingCount)
                .Select(message => message.Id)
                .ToList();
        }

        return await context.BrokerMessages
            .AsNoTracking()
            .Where(message =>
                !message.IsArchived &&
                (message.Status == BrokerMessageStatus.Pending ||
                 message.Status == BrokerMessageStatus.Failed ||
                 (message.Status == BrokerMessageStatus.Processing && message.LockedUntil < now)) &&
                (message.LockedUntil == null || message.LockedUntil < now))
            .OrderBy(message => message.CreatedDate)
            .Take(this.options.ProcessingCount)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken)
            .AnyContext();
    }

    private async Task ProcessMessageAsync(Guid brokerMessageId, CancellationToken cancellationToken)
    {
        var claimedMessage = await this.TryClaimMessageAsync(brokerMessageId, cancellationToken);
        if (claimedMessage is null)
        {
            return;
        }

        using var renewalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewalTask = this.StartLeaseRenewalAsync(brokerMessageId, renewalCts.Token);

        try
        {
            await this.broker.ProcessStoredMessageAsync(claimedMessage, cancellationToken);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(ex, "{LogKey} broker message processing crashed after lease acquisition (messageId={BrokerMessageId}, lockedBy={LockedBy})", Constants.LogKey, brokerMessageId, this.instanceName);
            this.MarkMessageFailed(claimedMessage, ex.Message);
        }
        finally
        {
            renewalCts.Cancel();
        }

        var leaseMaintained = await renewalTask.AnyContext();
        if (!leaseMaintained)
        {
            this.logger.LogWarning("{LogKey} broker message lease was lost while processing, final state will not be persisted (messageId={BrokerMessageId}, lockedBy={LockedBy})", Constants.LogKey, brokerMessageId, this.instanceName);
            return;
        }

        await this.FinalizeClaimedMessageAsync(claimedMessage, cancellationToken);
    }

    private async Task<BrokerMessage> TryClaimMessageAsync(Guid brokerMessageId, CancellationToken cancellationToken)
    {
        using var lease = this.CreateContextLease(out var context);
        var now = DateTimeOffset.UtcNow;
        var lockedUntil = now.Add(this.options.LeaseDuration);
        var concurrencyVersion = Guid.NewGuid();

        if (this.SupportsExecuteUpdate(context))
        {
            var claimed = await context.BrokerMessages
                .Where(message =>
                    message.Id == brokerMessageId &&
                    !message.IsArchived &&
                    (message.Status == BrokerMessageStatus.Pending ||
                     message.Status == BrokerMessageStatus.Failed ||
                     (message.Status == BrokerMessageStatus.Processing && message.LockedUntil < now)) &&
                    (message.LockedUntil == null || message.LockedUntil < now))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(message => message.LockedBy, this.instanceName)
                        .SetProperty(message => message.LockedUntil, lockedUntil)
                        .SetProperty(message => message.Status, BrokerMessageStatus.Processing)
                        .SetProperty(message => message.ProcessingStartedDate, now)
                        .SetProperty(message => message.ConcurrencyVersion, concurrencyVersion),
                    cancellationToken)
                .AnyContext();

            if (claimed == 0)
            {
                this.logger.LogDebug("{LogKey} broker message lease claim skipped because another worker already acquired or changed the row (messageId={BrokerMessageId}, lockedBy={LockedBy})", Constants.LogKey, brokerMessageId, this.instanceName);
                return null;
            }

            return await context.BrokerMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(message => message.Id == brokerMessageId, cancellationToken)
                .AnyContext();
        }

        var brokerMessage = await context.BrokerMessages.FirstOrDefaultAsync(message => message.Id == brokerMessageId, cancellationToken).AnyContext();
        if (brokerMessage is null ||
            brokerMessage.IsArchived ||
            !((brokerMessage.Status == BrokerMessageStatus.Pending ||
               brokerMessage.Status == BrokerMessageStatus.Failed ||
               (brokerMessage.Status == BrokerMessageStatus.Processing && brokerMessage.LockedUntil < now)) &&
              (brokerMessage.LockedUntil == null || brokerMessage.LockedUntil < now)))
        {
            this.logger.LogDebug("{LogKey} broker message lease claim skipped because another worker already acquired or changed the row (messageId={BrokerMessageId}, lockedBy={LockedBy})", Constants.LogKey, brokerMessageId, this.instanceName);
            return null;
        }

        brokerMessage.LockedBy = this.instanceName;
        brokerMessage.LockedUntil = lockedUntil;
        brokerMessage.Status = BrokerMessageStatus.Processing;
        brokerMessage.ProcessingStartedDate = now;
        brokerMessage.ConcurrencyVersion = concurrencyVersion;

        try
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
            return brokerMessage;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            this.logger.LogDebug(ex, "{LogKey} broker message lease claim lost due to optimistic concurrency (messageId={BrokerMessageId}, lockedBy={LockedBy})", Constants.LogKey, brokerMessageId, this.instanceName);
            return null;
        }
    }

    private Task<bool> StartLeaseRenewalAsync(Guid brokerMessageId, CancellationToken cancellationToken)
    {
        var renewalInterval = this.GetLeaseRenewalInterval();
        if (renewalInterval <= TimeSpan.Zero)
        {
            return Task.FromResult(true);
        }

        return this.RenewLeaseAsync(brokerMessageId, renewalInterval, cancellationToken);
    }

    private async Task<bool> RenewLeaseAsync(Guid brokerMessageId, TimeSpan renewalInterval, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(renewalInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var renewed = await this.TryRenewLeaseAsync(brokerMessageId, cancellationToken);
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

    private async Task<bool> TryRenewLeaseAsync(Guid brokerMessageId, CancellationToken cancellationToken)
    {
        using var lease = this.CreateContextLease(out var context);
        var lockedUntil = DateTimeOffset.UtcNow.Add(this.options.LeaseDuration);
        var concurrencyVersion = Guid.NewGuid();

        if (this.SupportsExecuteUpdate(context))
        {
            var renewed = await context.BrokerMessages
                .Where(message => message.Id == brokerMessageId && message.LockedBy == this.instanceName)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(message => message.LockedUntil, lockedUntil)
                        .SetProperty(message => message.ConcurrencyVersion, concurrencyVersion),
                    cancellationToken)
                .AnyContext();

            if (renewed == 0)
            {
                this.logger.LogWarning("{LogKey} broker message lease renewal skipped because ownership was lost (messageId={BrokerMessageId}, lockedBy={LockedBy})", Constants.LogKey, brokerMessageId, this.instanceName);
                return false;
            }

            return true;
        }

        var brokerMessage = await context.BrokerMessages.FirstOrDefaultAsync(message => message.Id == brokerMessageId, cancellationToken).AnyContext();
        if (brokerMessage is null || !string.Equals(brokerMessage.LockedBy, this.instanceName, StringComparison.Ordinal))
        {
            this.logger.LogWarning("{LogKey} broker message lease renewal skipped because ownership was lost (messageId={BrokerMessageId}, lockedBy={LockedBy})", Constants.LogKey, brokerMessageId, this.instanceName);
            return false;
        }

        brokerMessage.LockedUntil = lockedUntil;
        brokerMessage.ConcurrencyVersion = concurrencyVersion;

        try
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            this.logger.LogWarning(ex, "{LogKey} broker message lease renewal lost due to optimistic concurrency (messageId={BrokerMessageId}, lockedBy={LockedBy})", Constants.LogKey, brokerMessageId, this.instanceName);
            return false;
        }
    }

    private async Task FinalizeClaimedMessageAsync(BrokerMessage brokerMessage, CancellationToken cancellationToken)
    {
        using var lease = this.CreateContextLease(out var context);
        var storedMessage = await context.BrokerMessages.FirstOrDefaultAsync(message => message.Id == brokerMessage.Id, cancellationToken).AnyContext();
        if (storedMessage is null)
        {
            return;
        }

        if (!string.Equals(storedMessage.LockedBy, this.instanceName, StringComparison.Ordinal))
        {
            this.logger.LogWarning("{LogKey} broker message lease ownership changed before finalize (messageId={BrokerMessageId}, lockedBy={LockedBy}, currentLockedBy={CurrentLockedBy})", Constants.LogKey, brokerMessage.Id, this.instanceName, storedMessage.LockedBy);
            return;
        }

        this.ApplyProcessedState(storedMessage, brokerMessage);
        await this.FinalizeClaimedMessageAsync(context, storedMessage, cancellationToken);
    }

    private async Task FinalizeClaimedMessageAsync(TContext context, BrokerMessage brokerMessage, CancellationToken cancellationToken)
    {
        brokerMessage.LockedBy = null;
        brokerMessage.LockedUntil = null;

        if (this.ShouldArchive(brokerMessage))
        {
            brokerMessage.IsArchived = true;
            brokerMessage.ArchivedDate = DateTimeOffset.UtcNow;
        }

        brokerMessage.AdvanceConcurrencyVersion();

        try
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            this.logger.LogWarning(ex, "{LogKey} broker message finalize lost due to optimistic concurrency (messageId={BrokerMessageId}, lockedBy={LockedBy})", Constants.LogKey, brokerMessage.Id, this.instanceName);
        }
    }

    private void ApplyProcessedState(BrokerMessage target, BrokerMessage source)
    {
        target.Status = source.Status;
        target.LastError = source.LastError;
        target.ProcessedDate = source.ProcessedDate;
        target.IsArchived = source.IsArchived;
        target.ArchivedDate = source.ArchivedDate;
        target.HandlerStates = source.HandlerStates
            .Select(state => new BrokerMessageHandlerState
            {
                SubscriptionKey = state.SubscriptionKey,
                HandlerType = state.HandlerType,
                Status = state.Status,
                AttemptCount = state.AttemptCount,
                LastError = state.LastError,
                ProcessedDate = state.ProcessedDate
            })
            .ToList();
    }

    private void MarkMessageFailed(BrokerMessage brokerMessage, string error)
    {
        brokerMessage.Status = BrokerMessageStatus.Failed;
        brokerMessage.LastError = error;
        brokerMessage.ProcessedDate = null;

        foreach (var handlerState in brokerMessage.HandlerStates.Where(state => state.Status == BrokerMessageHandlerStatus.Processing))
        {
            handlerState.Status = BrokerMessageHandlerStatus.Failed;
            handlerState.LastError = error;
            handlerState.ProcessedDate = null;
        }
    }

    private bool ShouldArchive(BrokerMessage brokerMessage)
    {
        if (!this.options.AutoArchiveAfter.HasValue || !brokerMessage.ProcessedDate.HasValue)
        {
            return false;
        }

        return this.options.AutoArchiveStatuses.SafeAny(status => status == brokerMessage.Status) &&
            brokerMessage.ProcessedDate.Value.Add(this.options.AutoArchiveAfter.Value) <= DateTimeOffset.UtcNow;
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

    private bool CanClaimMessage(BrokerMessage message, DateTimeOffset now)
    {
        return message is not null &&
            !message.IsArchived &&
            (message.Status == BrokerMessageStatus.Pending ||
             message.Status == BrokerMessageStatus.Failed ||
             (message.Status == BrokerMessageStatus.Processing && message.LockedUntil < now)) &&
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
