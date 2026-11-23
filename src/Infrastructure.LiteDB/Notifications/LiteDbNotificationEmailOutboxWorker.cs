// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Claims emails at delivery time, renews their ownership, and purges through native status selection.</summary>
/// <param name="storage">The native notification storage and operational service.</param>
/// <param name="scopes">The scope factory for notification transports and audit behaviors.</param>
/// <param name="options">The frozen notification worker settings.</param>
/// <param name="clock">The retention clock.</param>
/// <param name="loggerFactory">The optional structured logger factory.</param>
public sealed class LiteDbNotificationEmailOutboxWorker(LiteDbNotificationStorageProvider storage, IServiceScopeFactory scopes,
    OutboxNotificationEmailOptions options, TimeProvider clock, ILoggerFactory loggerFactory = null) : IOutboxNotificationEmailWorker
{
    private readonly ILogger<LiteDbNotificationEmailOutboxWorker> logger = loggerFactory?.CreateLogger<LiteDbNotificationEmailOutboxWorker>() ?? NullLogger<LiteDbNotificationEmailOutboxWorker>.Instance;

    /// <inheritdoc />
    public async Task ProcessAsync(string messageId = null, CancellationToken cancellationToken = default)
    {
        Guid? preferredId = string.IsNullOrWhiteSpace(messageId) ? null : Guid.Parse(messageId);
        var ids = await storage.GetPendingIdsAsync(options.ProcessingCount, preferredId, cancellationToken).ConfigureAwait(false);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claim = await storage.TryClaimAsync(id, cancellationToken).ConfigureAwait(false);
            if (claim is null) { continue; }

            await this.DeliverAsync(claim, cancellationToken).ConfigureAwait(false);
        }

        if (options.AutoArchiveAfter.HasValue && options.AutoArchiveStatuses?.Any() == true)
        {
            await storage.AutoArchiveMessagesAsync(clock.GetUtcNow().Subtract(options.AutoArchiveAfter.Value), options.AutoArchiveStatuses, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default) => storage.PurgeMessagesAsync(
        statuses: processedOnly ? [EmailMessageStatus.Sent, EmailMessageStatus.Failed] : null, cancellationToken: cancellationToken);

    private async Task DeliverAsync(LiteDbNotificationEmailClaim claim, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var delivery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!await storage.RenewAsync(claim, cancellationToken).ConfigureAwait(false)) { return; }

        var renewing = this.RenewAsync(claim, stop.Token, delivery);
        try
        {
            await this.ExecuteAuditedAsync(claim.Message, delivery.Token).ConfigureAwait(false);
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
            await renewing.ConfigureAwait(false);
        }
    }

    private async Task RenewAsync(LiteDbNotificationEmailClaim claim, CancellationToken cancellationToken, CancellationTokenSource delivery)
    {
        try
        {
            var interval = TimeSpan.FromMilliseconds(Math.Clamp(options.LeaseDuration.TotalMilliseconds / 3, 1, int.MaxValue));
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await storage.RenewAsync(claim, cancellationToken).ConfigureAwait(false))
                {
                    await delivery.CancelAsync().ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            this.logger.LogWarning(exception, "[{LogKey}] notification lease renewal failed (messageId={MessageId})", "LiteDB", claim.Message.Id);
            await delivery.CancelAsync().ConfigureAwait(false);
        }
    }

    private async Task ExecuteAuditedAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var operation = new NotificationOperationContext
        {
            Operation = "OutboxExecution", MessageId = message.Id.ToString("D"), Properties = message.Properties, IsWorker = true,
            Attempt = message.RetryCount < options.RetryCount ? message.RetryCount + 1 : null,
            SuccessStatus = "SmtpAccepted", FailureStatus = message.RetryCount >= options.RetryCount ? "RetryLimit" : "Failed", CancellationToken = cancellationToken
        };
        Func<Task<Result>> next = () => this.SendAsync(message, scope.ServiceProvider, cancellationToken);
        foreach (var behavior in AuditSourceExecutor.DistinctAuditing<INotificationOperationBehavior, AuditingNotificationOperationBehavior>(
            scope.ServiceProvider.GetServices<INotificationOperationBehavior>()).OrderBy(behavior => behavior is AuditingNotificationOperationBehavior ? 0 : 1).Reverse())
        {
            var inner = next;
            next = () => behavior.ExecuteAsync(operation, inner);
        }

        var result = await AuditSourceExecutor.RunIsolatedAsync(scopes, "Email", operation.MessageId, AuditHandoffMetadata.Read(message.Properties), null, next).ConfigureAwait(false);
        if (result.IsFailure)
        {
            this.logger.LogWarning("[{LogKey}] notification delivery failed (messageId={MessageId})", "LiteDB", message.Id);
        }
    }

    private async Task<Result> SendAsync(EmailMessage message, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (message.RetryCount >= options.RetryCount)
        {
            message.Status = EmailMessageStatus.Failed;
            message.SentAt = null;
            message.Properties["ProcessMessage"] = $"Max retries reached (attempts={message.RetryCount})";
            var update = await storage.UpdateAsync(message, cancellationToken).ConfigureAwait(false);
            return update.IsFailure ? update : Result.Failure(new Error("Notification retry limit reached."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            message.Properties["Outbox"] = true;
            message.RetryCount++;
            return await services.GetRequiredService<INotificationService<EmailMessage>>().SendAsync(
                message, new NotificationSendOptions { SendImmediately = true }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "[{LogKey}] notification delivery threw (messageId={MessageId})", "LiteDB", message.Id);
            message.Status = EmailMessageStatus.Failed;
            message.Properties["ProcessMessage"] = $"[{exception.GetType().Name}] {exception.Message}";
            var update = await storage.UpdateAsync(message, cancellationToken).ConfigureAwait(false);
            return update.IsFailure ? update : Result.Failure(new ExceptionError(exception));
        }
    }
}
