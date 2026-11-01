// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Notifications;

using BridgingIT.DevKit.Common;

/// <summary>Wraps one complete notification operation without exposing addresses, headers, body, or attachments.</summary>
/// <example><code>public Task&lt;Result&gt; ExecuteAsync(NotificationOperationContext context, Func&lt;Task&lt;Result&gt;&gt; next) => next();</code></example>
public interface INotificationOperationBehavior
{
    /// <summary>Invokes actual notification work once and awaits its cleanup.</summary>
    /// <example><code>var result = await behavior.ExecuteAsync(context, next);</code></example>
    Task<Result> ExecuteAsync(NotificationOperationContext context, Func<Task<Result>> next);
}

/// <summary>Contains source-owned operation facts and opaque causal transport metadata.</summary>
/// <example><code>var context = new NotificationOperationContext { Operation = "Queue", MessageId = id };</code></example>
public sealed record NotificationOperationContext
{
    /// <summary>The fixed operation label, such as Queue, Send, SmtpAttempt, or OutboxExecution.</summary>
    /// <example><code>var operation = context.Operation;</code></example>
    public string Operation { get; init; }

    /// <summary>The native message identifier.</summary>
    /// <example><code>var id = context.MessageId;</code></example>
    public string MessageId { get; init; }

    /// <summary>The existing source transport bag, used only for the reserved protected token by the built-in adapter.</summary>
    /// <example><code>var token = AuditHandoffMetadata.Read(context.Properties);</code></example>
    public IDictionary<string, object> Properties { get; init; }

    /// <summary>Whether the source is executing an independently claimed outbox message.</summary>
    /// <example><code>var worker = context.IsWorker;</code></example>
    public bool IsWorker { get; init; }

    /// <summary>The native one-based SMTP or outbox attempt, when actually observed.</summary>
    /// <example><code>var attempt = context.Attempt;</code></example>
    public int? Attempt { get; set; }

    /// <summary>The source's successful operation status. It does not assert recipient delivery.</summary>
    /// <example><code>context.SuccessStatus = "SmtpAccepted";</code></example>
    public string SuccessStatus { get; set; } = "Completed";

    /// <summary>The source's failed operation status, such as RetryLimit or Failed.</summary>
    /// <example><code>var status = context.FailureStatus;</code></example>
    public string FailureStatus { get; init; } = "Failed";

    /// <summary>Whether the SMTP server accepted the actual send, independent of later disconnect cleanup.</summary>
    /// <example><code>context.SmtpAccepted = true;</code></example>
    public bool SmtpAccepted { get; set; }

    /// <summary>The operation cancellation signal, used to classify source APIs that return cancellation as a Result.</summary>
    /// <example><code>var canceled = context.CancellationToken.IsCancellationRequested;</code></example>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>Selects notification admission and independent worker recording separately.</summary>
/// <example><code>var options = new NotificationAuditOptions { Operations = true, Workers = true };</code></example>
public sealed record NotificationAuditOptions
{
    /// <summary>Records selected Queue, Send, and actual SMTP attempts.</summary>
    /// <example><code>var enabled = options.Operations;</code></example>
    public bool Operations { get; init; } = true;

    /// <summary>Records independently claimed outbox-message executions.</summary>
    /// <example><code>var enabled = options.Workers;</code></example>
    public bool Workers { get; init; } = true;
}
