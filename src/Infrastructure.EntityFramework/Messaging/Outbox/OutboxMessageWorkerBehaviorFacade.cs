// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using Application.Messaging;

/// <summary>
/// Represents outbox message worker behavior facade.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public static class OutboxMessageWorkerBehaviorFacade<TContext>
    where TContext : DbContext, IOutboxMessageContext
{
    /// <summary>
    /// Creates publish behavior for test.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="context">The context for the operation.</param>
    /// <param name="messageQueue">The message queue used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static OutboxMessagePublisherBehavior<TContext> CreatePublishBehaviorForTest(
        ILoggerFactory loggerFactory,
        TContext context,
        IOutboxMessageQueue messageQueue = null,
        OutboxMessageOptions options = null)
    {
        return new OutboxMessagePublisherBehavior<TContext>(loggerFactory, context, messageQueue, options);
    }
}
