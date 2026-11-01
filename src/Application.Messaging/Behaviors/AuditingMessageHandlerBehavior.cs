// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Observes actual handler attempts within a finite independent consumer execution.</summary>
/// <example><code>builder.WithBehavior&lt;AuditingMessageHandlerBehavior&gt;();</code></example>
public sealed class AuditingMessageHandlerBehavior(IServiceScopeFactory scopes) : IMessageHandlerBehavior
{
    private readonly AsyncLocal<AttemptCounter> current = new();

    /// <inheritdoc />
    public Task Handle<TMessage>(TMessage message, CancellationToken cancellationToken, object handler, MessageHandlerDelegate next) where TMessage : IMessage
        => AuditSourceExecutor.RunAsync(scopes, builder => builder.For(message.GetType(), "Handle").Kind("MessageHandler"), async () =>
        {
            await next().ConfigureAwait(false);
            return true;
        }, new() { Feature = "Messaging", RequestType = message.GetType().FullName, HandlerType = handler.GetType().FullName,
            RequestId = message.MessageId, Attempt = this.current.Value?.Next() ?? 1, ExecutionStatus = "Invoked" }, failed: (scope, error) =>
        {
            var actual = error;
            for (var depth = 0; depth < 4 && actual is System.Reflection.TargetInvocationException { InnerException: not null }; depth++) actual = actual.InnerException;
            var canceled = actual is OperationCanceledException cancellation && cancellation.CancellationToken.IsCancellationRequested;
            this.current.Value?.Failure(canceled);
            if (canceled) scope.Complete(AuditOutcome.Canceled);
        });

    /// <summary>Isolates actual delivery before resolving handlers, observing returned failures and cancellation separately from producer acceptance.</summary>
    /// <example><code>await behavior.ExecuteDeliveryAsync(message, ProcessOnceAsync, cancellationToken);</code></example>
    public async Task<bool> ExecuteDeliveryAsync(IMessage message, Func<Task<bool>> next, CancellationToken cancellationToken = default, string correlationId = null)
    {
        var previous = this.current.Value;
        var attempts = new AttemptCounter();
        this.current.Value = attempts;
        try
        {
            return await AuditSourceExecutor.RunWorkerAsync(scopes, "Messaging", message.MessageId, AuditHandoffMetadata.Read(message.Properties),
                builder => builder.For(message.GetType(), "Consume").Kind("MessageConsumer"), next,
                new() { Feature = "Messaging", RequestType = message.GetType().FullName, RequestId = message.MessageId }, completed: (scope, succeeded) =>
                {
                    if (!succeeded) scope.Complete(cancellationToken.IsCancellationRequested || attempts.LastCanceled ? AuditOutcome.Canceled : AuditOutcome.Failed);
                    scope.SetDetail("message.handlerAttempts", AuditValue.From((long)attempts.Count));
                    (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = !succeeded ? "DeliveryFailed" : attempts.Count == 0 ? "ShortCircuited" : "Processed" });
                }, correlationId: correlationId).ConfigureAwait(false);
        }
        finally { this.current.Value = previous; }
    }

    private sealed class AttemptCounter
    {
        private int count;
        private int canceled;

        public bool LastCanceled => Volatile.Read(ref this.canceled) == 1;

        public void Failure(bool canceled) => Volatile.Write(ref this.canceled, canceled ? 1 : 0);

        public int Count => Volatile.Read(ref this.count);

        public int Next() => Interlocked.Increment(ref this.count);
    }
}
