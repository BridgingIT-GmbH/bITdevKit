// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

public static partial class DbContextBuilderContextExtensions
{
    public static DbContextBuilderContext<TContext> WithOutboxDomainEventService<TContext>(this DbContextBuilderContext<TContext> context, Builder<OutboxDomainEventOptionsBuilder, OutboxDomainEventOptions> optionsBuilder)
        where TContext : DbContext, IOutboxDomainEventContext
    {
        context.Services.AddOutboxDomainEventService<TContext>(optionsBuilder);

        return context;
    }

    public static DbContextBuilderContext<TContext> WithOutboxDomainEventService<TContext>(this DbContextBuilderContext<TContext> context, OutboxDomainEventOptions options = null)
        where TContext : DbContext, IOutboxDomainEventContext
    {
        context.Services.AddOutboxDomainEventService<TContext>(options);

        return context;
    }
}
