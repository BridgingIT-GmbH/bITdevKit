// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Notifications;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Records notification admission, SMTP acceptance and worker executions without email contents.</summary>
/// <example><code>services.AddNotificationService&lt;EmailMessage&gt;(configure: builder => builder.WithAuditing());</code></example>
public sealed class AuditingNotificationOperationBehavior(IServiceScopeFactory scopes, NotificationAuditOptions options = null) : INotificationOperationBehavior
{
    /// <inheritdoc />
    public Task<Result> ExecuteAsync(NotificationOperationContext context, Func<Task<Result>> next)
    {
        var selection = options ?? new();
        if (context.IsWorker ? !selection.Workers : !selection.Operations) return next();
        void Configure(AuditScopeOptionsBuilder builder) => builder.Key("email." + context.Operation.ToLowerInvariant()).Kind(context.IsWorker ? "EmailWorker" : "Email" + context.Operation);
        var metadata = new AuditSourceMetadata { Feature = "Email", RequestId = context.MessageId, Attempt = context.Attempt, ExecutionStatus = "Invoked" };
        void Completed(IAuditScope scope, Result result)
        {
            var status = result.IsSuccess ? context.SuccessStatus : context.CancellationToken.IsCancellationRequested ? "Canceled" : context.FailureStatus;
            scope.Complete(result.IsSuccess ? AuditOutcome.Succeeded : context.CancellationToken.IsCancellationRequested ? AuditOutcome.Canceled : context.FailureStatus == "RetryLimit" ? AuditOutcome.Rejected : AuditOutcome.Failed);
            (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = context.SmtpAccepted ? "SmtpAccepted" : status, Attempt = context.Attempt });
            if (context.Operation == "SmtpAttempt") scope.SetDetail("notification.smtpAccepted", AuditValue.From(context.SmtpAccepted));
        }

        void Failed(IAuditScope scope, Exception error)
        {
            if (context.Operation != "SmtpAttempt") return;
            scope.SetDetail("notification.smtpAccepted", AuditValue.From(context.SmtpAccepted));
            (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = context.SmtpAccepted ? "SmtpAccepted" : error is OperationCanceledException canceled && canceled.CancellationToken.IsCancellationRequested ? "Canceled" : "Failed", Attempt = context.Attempt });
        }

        return context.IsWorker
            ? AuditSourceExecutor.RunWorkerAsync(scopes, "Email", context.MessageId, AuditHandoffMetadata.Read(context.Properties), Configure, next, metadata, completed: Completed, failed: Failed)
            : context.Operation == "SmtpAttempt"
                ? AuditSourceExecutor.RunAsync(scopes, Configure, next, metadata, completed: Completed, failed: Failed)
                : AuditSourceExecutor.RunProducerAsync(scopes, "Email", context.MessageId, context.Properties, Configure, next, metadata, completed: Completed, failed: Failed);
    }
}
