// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Hosting;

public sealed partial class LiteDbMessageBroker
{
    /// <summary>Claims a bounded batch and delivers each message outside its native transaction.</summary>
    /// <param name="cancellationToken">Cancels acquisition and external delivery.</param>
    /// <param name="messageId">An optional exact logical message identity.</param>
    /// <returns>The completed batch task.</returns>
    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default, string messageId = null)
    {
        if (!this.options.Enabled) { return; }

        var rows = await this.database.ExecuteAsync(() =>
        {
            var now = this.clock.GetUtcNow().UtcTicks;
            var expression = "$.Archived = false AND ($.Ready = true OR ($.Waiting = true AND $.ExpiresTicks > 0 AND $.ExpiresTicks <= @0)) AND $.LeaseUntilTicks <= @0 AND $.NextAttemptTicks <= @0";
            var parameters = new List<BsonValue> { now };
            foreach (var type in this.paused.Keys)
            {
                parameters.Add(type);
                expression += $" AND $.Type != @{parameters.Count - 1}";
            }

            if (messageId is not null) { parameters.Add(messageId); expression += $" AND $.MessageId = @{parameters.Count - 1}"; }

            var predicate = BsonExpression.Create(expression, parameters.ToArray());
            var candidates = this.database.Query<LiteDbBrokerMessage>("messages", predicate, this.options.BatchSize);
            foreach (var row in candidates)
            {
                row.LeaseToken = Guid.NewGuid();
                row.LockedBy = Environment.MachineName;
                row.LockedUntil = this.clock.GetUtcNow().Add(this.options.LeaseDuration);
                row.Status = BrokerMessageStatus.Processing;
            }

            return Task.FromResult(candidates);
        }, cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await this.SaveClaimedAsync(row, false).ConfigureAwait(false)) { continue; }

            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var delivery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var renewing = this.RenewLeaseAsync(row, stop.Token, delivery);
            try
            {
                await this.ProcessStoredMessageAsync(row, delivery.Token, async () =>
                {
                    if (!await this.SaveClaimedAsync(row, false).ConfigureAwait(false)) { throw new OperationCanceledException("The delivery lease was lost.", delivery.Token); }
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (delivery.IsCancellationRequested || cancellationToken.IsCancellationRequested) { row.Status = BrokerMessageStatus.Failed; }
            catch (Exception exception)
            {
                row.ProcessingFailureCount++;
                var error = exception.Message.Length > 4000 ? exception.Message[..4000] : exception.Message;
                if (row.ProcessingFailureCount >= this.options.MaxDeliveryAttempts) { this.DeadLetterMessage(row, error); }
                else { row.Status = BrokerMessageStatus.Failed; row.LastError = error; }
            }
            finally
            {
                await stop.CancelAsync().ConfigureAwait(false);
                try { await renewing.ConfigureAwait(false); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            }

            await this.SaveClaimedAsync(row, true).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private Task<bool> SaveClaimedAsync(LiteDbBrokerMessage row, bool release) => this.database.ExecuteAsync(() =>
    {
        if (!this.messages.TryGetValue(row.Id, out var current) || current.LeaseToken != row.LeaseToken || current.LockedUntil <= this.clock.GetUtcNow()) { return Task.FromResult(false); }

        if (release)
        {
            row.LeaseToken = null;
            row.LockedBy = null;
            row.LockedUntil = null;
            row.NextAttemptUtc = row.Status is BrokerMessageStatus.Pending or BrokerMessageStatus.Failed ? this.clock.GetUtcNow().Add(this.options.ProcessingInterval) : null;
        }
        else { row.LockedUntil = current.LockedUntil; }

        this.messages[row.Id] = row;
        return Task.FromResult(true);
    });

    private async Task RenewLeaseAsync(LiteDbBrokerMessage row, CancellationToken cancellationToken, CancellationTokenSource delivery)
    {
        try
        {
            using var timer = new PeriodicTimer(this.options.LeaseRenewalInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var owned = await this.database.ExecuteAsync(() =>
                {
                    if (!this.messages.TryGetValue(row.Id, out var current) || current.LeaseToken != row.LeaseToken || current.LockedUntil <= this.clock.GetUtcNow()) { return Task.FromResult(false); }

                    current.LockedUntil = this.clock.GetUtcNow().Add(this.options.LeaseDuration);
                    return Task.FromResult(true);
                }, cancellationToken).ConfigureAwait(false);
                if (!owned) { await delivery.CancelAsync().ConfigureAwait(false); return; }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch { await delivery.CancelAsync().ConfigureAwait(false); }
    }
}

/// <summary>Runs the native durable messaging poller through the host lifecycle.</summary>
/// <param name="broker">The selected durable broker.</param>
/// <param name="options">The frozen polling settings.</param>
public sealed class LiteDbMessageBrokerWorker(LiteDbMessageBroker broker, LiteDbMessageBrokerOptions options) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) { return; }

        try
        {
            await Task.Delay(options.StartupDelay, stoppingToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(options.ProcessingInterval);
            do { await broker.ProcessPendingAsync(stoppingToken).ConfigureAwait(false); }

            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
