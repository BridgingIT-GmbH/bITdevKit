// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Records selected queueing admission intent without copying message contents or accepting payload attribution.</summary>
/// <example><code>builder.WithBehavior&lt;AuditingQueueEnqueuerBehavior&gt;();</code></example>
public sealed class AuditingQueueEnqueuerBehavior(IServiceScopeFactory scopes) : IQueueEnqueuerBehavior
{
    /// <inheritdoc />
    public Task Enqueue(IQueueMessage message, CancellationToken cancellationToken, QueueEnqueuerDelegate next)
        => AuditSourceExecutor.RunProducerAsync(scopes, "Queueing", message.MessageId, message.Properties,
            builder => builder.For(message.GetType(), "Enqueue").Kind("QueueEnqueuer"), async () =>
            {
                await next().ConfigureAwait(false);
                return true;
            }, new() { Feature = "Queueing", RequestType = message.GetType().FullName, RequestId = message.MessageId },
            completed: (scope, _) => (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = "Accepted" }), failed: (scope, error) =>
            {
                if (error is FluentValidation.ValidationException)
                {
                    scope.Complete(AuditOutcome.Rejected);
                    (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = "Rejected" });
                }
            });
}
