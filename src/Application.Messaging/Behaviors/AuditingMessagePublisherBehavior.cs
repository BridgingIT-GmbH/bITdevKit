// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Records selected messaging admission intent without copying message contents or accepting payload attribution.</summary>
/// <example><code>builder.WithBehavior&lt;AuditingMessagePublisherBehavior&gt;();</code></example>
public sealed class AuditingMessagePublisherBehavior(IServiceScopeFactory scopes) : IMessagePublisherBehavior
{
    private readonly AsyncLocal<bool> deferred = new();

    /// <summary>Owns one independent deferred transport attempt while preserving the original protected admission link.</summary>
    /// <example><code>await auditing.PublishDeferredAsync(message, () => broker.Publish(message, cancellationToken));</code></example>
    public async Task PublishDeferredAsync(IMessage message, Func<Task> next)
    {
        var previous = this.deferred.Value;
        this.deferred.Value = true;
        try
        {
            await AuditSourceExecutor.RunWorkerAsync(scopes, "Messaging", message.MessageId, AuditHandoffMetadata.Read(message.Properties),
                builder => builder.For(message.GetType(), "Transport").Kind("MessageTransport"), async () =>
                {
                    await next().ConfigureAwait(false);
                    return true;
                }, new() { Feature = "Messaging", RequestType = message.GetType().FullName, RequestId = message.MessageId, ExecutionStatus = "DeferredTransportAttempt" }, correlationId: CorrelationIdGenerator.GetOrCreate(message.Properties, useAmbient: false)).ConfigureAwait(false);
        }
        finally { this.deferred.Value = previous; }
    }

    /// <inheritdoc />
    public Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken, MessagePublisherDelegate next) where TMessage : IMessage
        => AuditSourceExecutor.RunProducerAsync(scopes, "Messaging", message.MessageId, message.Properties,
            builder => builder.For(message.GetType(), "Publish").Kind("MessagePublisher"), async () =>
            {
                await next().ConfigureAwait(false);
                return true;
            }, new() { Feature = "Messaging", RequestType = message.GetType().FullName, RequestId = message.MessageId },
            completed: (scope, _) => (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = this.deferred.Value ? "TransportAccepted" : "Accepted" }), failed: (scope, error) =>
            {
                if (error is FluentValidation.ValidationException)
                {
                    scope.Complete(AuditOutcome.Rejected);
                    (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = "Rejected" });
                }
            });
}
