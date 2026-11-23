// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Hosting;

public sealed partial class LiteDbQueueBroker
{
    /// <summary>Claims a bounded batch and delivers each message outside its native transaction.</summary>
    /// <param name="cancellationToken">Cancels acquisition and external delivery.</param>
    /// <returns>The completed batch task.</returns>
    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!this.options.Enabled) { return; }

        var rows = await this.database.ExecuteAsync(() =>
        {
            var now = this.clock.GetUtcNow().UtcTicks;
            var expression = "$.Archived = false AND ($.Ready = true OR ($.Waiting = true AND $.ExpiresTicks > 0 AND $.ExpiresTicks <= @0)) AND $.LeaseUntilTicks <= @0 AND $.NextAttemptTicks <= @0";
            var parameters = new List<BsonValue> { now };
            foreach (var type in this.paused.Keys)
            {
                parameters.Add(type.StartsWith("queue:", StringComparison.Ordinal) ? type[6..] : type);
                var field = type.StartsWith("queue:", StringComparison.Ordinal) ? "QueueName" : "Type";
                expression += $" AND $.{field} != @{parameters.Count - 1}";
            }

            var predicate = BsonExpression.Create(expression, parameters.ToArray());
            var candidates = this.database.Query<LiteDbQueueMessage>("messages", predicate, this.options.BatchSize);
            foreach (var row in candidates)
            {
                row.LeaseToken = Guid.NewGuid();
                row.LockedBy = Environment.MachineName;
                row.LockedUntil = this.clock.GetUtcNow().Add(this.options.LeaseDuration);
                row.Status = QueueMessageStatus.Processing;
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
                await this.ProcessStoredMessageAsync(row, delivery.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (delivery.IsCancellationRequested || cancellationToken.IsCancellationRequested) { row.Status = QueueMessageStatus.Failed; }
            catch (Exception exception)
            {
                row.ProcessingFailureCount++;
                var error = exception.Message.Length > 4000 ? exception.Message[..4000] : exception.Message;
                if (row.ProcessingFailureCount >= this.options.MaxDeliveryAttempts) { this.DeadLetterMessage(row, error); }
                else { row.Status = QueueMessageStatus.Failed; row.LastError = error; }
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

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!this.options.Enabled) { return; }

        await Task.Delay(this.options.StartupDelay, cancellationToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(this.options.ProcessingInterval);
        do { await this.ProcessPendingAsync(cancellationToken).ConfigureAwait(false); }

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    private Task<bool> SaveClaimedAsync(LiteDbQueueMessage row, bool release) => this.database.ExecuteAsync(() =>
    {
        if (!this.messages.TryGetValue(row.Id, out var current) || current.LeaseToken != row.LeaseToken || current.LockedUntil <= this.clock.GetUtcNow()) { return Task.FromResult(false); }

        if (release)
        {
            row.LeaseToken = null;
            row.LockedBy = null;
            row.LockedUntil = null;
            row.NextAttemptUtc = row.Status is QueueMessageStatus.Pending or QueueMessageStatus.Failed ? this.clock.GetUtcNow().Add(this.options.ProcessingInterval) : null;
        }
        else { row.LockedUntil = current.LockedUntil; }

        this.messages[row.Id] = row;
        return Task.FromResult(true);
    });

    private async Task RenewLeaseAsync(LiteDbQueueMessage row, CancellationToken cancellationToken, CancellationTokenSource delivery)
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
