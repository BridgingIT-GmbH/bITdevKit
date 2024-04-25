// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using Application.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public static class OutboxMessageWorkerBehaviorFascade<TContext>
    where TContext : DbContext, IOutboxMessageContext
{
    public static OutboxMessagePublisherBehavior<TContext> CreatePublishBehaviorForTest(ILoggerFactory loggerFactory,
        TContext context, IOutboxMessageQueue messageQueue = null, OutboxMessageOptions options = null)
    {
        return new OutboxMessagePublisherBehavior<TContext>(loggerFactory, context, messageQueue, options);
    }
}