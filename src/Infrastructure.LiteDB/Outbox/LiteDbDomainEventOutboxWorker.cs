// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Delivers bounded durable event batches using expiring claims and fenced completion.</summary>
/// <param name="services">The root scope factory used to isolate every native transaction and delivery.</param>
/// <param name="databaseName">The selected database registration.</param>
/// <param name="options">The frozen worker settings.</param>
/// <param name="timeProvider">The clock used to validate and renew leases.</param>
/// <param name="logger">The structured operational logger.</param>
public sealed class LiteDbDomainEventOutboxWorker(IServiceScopeFactory services, string databaseName,
    LiteDbDomainEventOutboxOptions options, TimeProvider timeProvider, ILogger<LiteDbDomainEventOutboxWorker> logger) : IOutboxDomainEventWorker
{
    private readonly Channel<bool> signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Signals a committed event to the immediate-mode background worker without performing I/O.</summary>
    public void Signal() => this.signals.Writer.TryWrite(true);

    /// <summary>Waits for the next immediate commit signal. Scheduled polling uses the existing DevKit outbox hosted service.</summary>
    /// <param name="cancellationToken">Cancels the background wait.</param>
    /// <returns>The wait task.</returns>
    public async Task WaitAsync(CancellationToken cancellationToken) => await this.signals.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task ProcessAsync(string eventId = null, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) { return; }

        Guid? requested = eventId is null ? null : Guid.Parse(eventId);
        // A failed event is attempted once per invocation; remaining retries are durable subsequent attempts.
        var seen = new HashSet<Guid>();
        for (var i = 0; i < Math.Min(options.BatchSize, options.ProcessingCount); i++)
        {
            var claimed = await this.NativeAsync(database =>
            {
                var now = timeProvider.GetUtcNow().UtcTicks;
                var collection = database.GetCollection<LiteDbOutboxDomainEvent>(options.CollectionName);
                var candidates = collection.Query().Where(x => !x.IsArchived && x.ProcessedTicks == null && x.LeaseUntilTicks <= now)
                    .OrderBy(x => x.CreatedTicks);
                if (requested.HasValue) { candidates = candidates.Where(x => x.Id == requested.Value); }

                // Exclude attempts already handled by this call before limiting, so a poison row cannot starve the batch.
                foreach (var id in seen) { var excluded = id; candidates = candidates.Where(x => x.Id != excluded); }

                var row = candidates.FirstOrDefault();
                if (row is null) { return null; }

                if (row.Attempts >= options.RetryCount)
                {
                    row.ProcessedTicks = now;
                    row.LastError ??= "The configured delivery attempt limit was reached.";
                }
                else
                {
                    row.Attempts++;
                    row.LeaseToken = Guid.NewGuid();
                    row.LeaseUntilTicks = timeProvider.GetUtcNow().Add(options.LeaseDuration).UtcTicks;
                }

                collection.Update(row);
                return row;
            }, cancellationToken).ConfigureAwait(false);
            if (claimed is null) { break; }

            seen.Add(claimed.Id);
            if (claimed.ProcessedTicks.HasValue) { continue; }

            await this.DeliverAsync(claimed, cancellationToken).ConfigureAwait(false);
            if (requested.HasValue) { break; }
        }
    }

    /// <inheritdoc />
    public Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default) =>
        this.MaintainAsync(false, processedOnly, cancellationToken);

    /// <inheritdoc />
    public Task ArchiveAsync(CancellationToken cancellationToken = default) => this.MaintainAsync(true, true, cancellationToken);

    private async Task DeliverAsync(LiteDbOutboxDomainEvent row, CancellationToken cancellationToken)
    {
        using var renewal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var delivery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewing = this.RenewAsync(row, renewal.Token, delivery);
        string failure = null;
        var succeeded = false;
        try
        {
            if (HashHelper.Compute(row.Content) != row.ContentHash) { throw new InvalidOperationException("The event payload integrity check failed."); }

            var type = Type.GetType(row.Type, throwOnError: true)!;
            var domainEvent = options.Serializer.Deserialize(row.Content, type) as IDomainEvent
                ?? throw new InvalidOperationException("The stored payload is not a domain event.");
            if (domainEvent.EventId != row.Id) { throw new InvalidOperationException("The stored payload event identity does not match its record."); }

            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.Properties ?? "{}")!;
            properties.TryGetValue(ModuleConstants.ActivityParentIdKey, out var parent);
            using var activity = new Activity("OUTBOX_PROCESS " + type.Name).SetParentId(parent.ValueKind == JsonValueKind.String ? parent.GetString() : null).Start();
            foreach (var pair in properties) { activity.AddBaggage(pair.Key, pair.Value.ToString()); }

            await using var scope = services.CreateAsyncScope();
            var notifier = scope.ServiceProvider.GetRequiredService<INotifier>();
            var result = await notifier.PublishDynamicAsync(domainEvent, new PublishOptions(), delivery.Token).ConfigureAwait(false);
            if (result.IsFailure) { throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Message))); }

            succeeded = true;
        }
        catch (OperationCanceledException) when (delivery.IsCancellationRequested) { failure = "Event delivery was interrupted or its lease was lost."; }
        catch (Exception exception)
        {
            failure = exception.Message.Length <= 4000 ? exception.Message : exception.Message[..4000];
            logger.LogWarning(exception, "[{LogKey}] domain event delivery failed (eventId={EventId}, attempt={Attempt})", "LiteDB", row.Id, row.Attempts);
        }
        finally
        {
            await renewal.CancelAsync().ConfigureAwait(false);
            try { await renewing.ConfigureAwait(false); }
            catch (OperationCanceledException) when (renewal.IsCancellationRequested) { }
        }

        await this.NativeAsync(database =>
        {
            var collection = database.GetCollection<LiteDbOutboxDomainEvent>(options.CollectionName);
            var current = collection.FindById(row.Id);
            var now = timeProvider.GetUtcNow().UtcTicks;
            if (current is null || current.LeaseToken != row.LeaseToken || current.LeaseUntilTicks <= now) { return false; }

            current.ProcessedTicks = succeeded || current.Attempts >= options.RetryCount ? now : null;
            current.LastError = failure;
            current.LeaseToken = null;
            current.LeaseUntilTicks = 0;
            return collection.Update(current);
        }, CancellationToken.None).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task RenewAsync(LiteDbOutboxDomainEvent row, CancellationToken cancellationToken, CancellationTokenSource delivery)
    {
        try
        {
            using var timer = new PeriodicTimer(options.LeaseRenewalInterval, timeProvider);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var renewed = await this.NativeAsync(database =>
                {
                    var collection = database.GetCollection<LiteDbOutboxDomainEvent>(options.CollectionName);
                    var current = collection.FindById(row.Id);
                    if (current is null || current.LeaseToken != row.LeaseToken || current.LeaseUntilTicks <= timeProvider.GetUtcNow().UtcTicks) { return false; }

                    current.LeaseUntilTicks = timeProvider.GetUtcNow().Add(options.LeaseDuration).UtcTicks;
                    return collection.Update(current);
                }, cancellationToken).ConfigureAwait(false);
                if (!renewed) { await delivery.CancelAsync().ConfigureAwait(false); return; }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "[{LogKey}] domain event lease renewal failed (eventId={EventId})", "LiteDB", row.Id);
            await delivery.CancelAsync().ConfigureAwait(false);
        }
    }

    private async Task MaintainAsync(bool archive, bool processedOnly, CancellationToken cancellationToken)
    {
        if (archive && !options.AutoArchiveAfter.HasValue) { return; }

        while (true)
        {
            var changed = await this.NativeAsync(database =>
            {
                var now = timeProvider.GetUtcNow();
                var threshold = archive ? now.Subtract(options.AutoArchiveAfter!.Value).UtcTicks : long.MaxValue;
                var collection = database.GetCollection<LiteDbOutboxDomainEvent>(options.CollectionName);
                var query = collection.Query().Where(x => x.LeaseUntilTicks <= now.UtcTicks);
                if (processedOnly) { query = query.Where(x => x.ProcessedTicks != null); }

                if (archive) { query = query.Where(x => !x.IsArchived && x.ProcessedTicks <= threshold); }

                var batch = query.Limit(options.BatchSize).ToList();
                foreach (var row in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (archive) { row.IsArchived = true; row.ArchivedTicks = now.UtcTicks; collection.Update(row); }
                    else { collection.Delete(row.Id); }
                }

                return batch.Count;
            }, cancellationToken).ConfigureAwait(false);
            if (changed < options.BatchSize) { return; }
        }
    }

    private async Task<T> NativeAsync<T>(Func<LiteDatabase, T> work, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>(databaseName);
        var result = await session.ExecuteAsync(async () => Result<T>.Success(await session.NativeAsync(work, cancellationToken).ConfigureAwait(false)), true, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { throw new LiteDbProviderException(result); }

        return result.Value;
    }
}
