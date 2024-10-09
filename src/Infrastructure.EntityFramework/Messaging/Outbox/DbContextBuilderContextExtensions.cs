// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Messaging;

public static partial class DbContextBuilderContextExtensions
{
    public static DbContextBuilderContext<TContext> WithOutboxMessageService<TContext>(
        this DbContextBuilderContext<TContext> context,
        Builder<OutboxMessageOptionsBuilder, OutboxMessageOptions> optionsBuilder)
        where TContext : DbContext, IOutboxMessageContext
    {
        context.Services.AddOutboxMessageService<TContext>(optionsBuilder);

        return context;
    }

    public static DbContextBuilderContext<TContext> WithOutboxMessageService<TContext>(
        this DbContextBuilderContext<TContext> context,
        OutboxMessageOptions options = null)
        where TContext : DbContext, IOutboxMessageContext
    {
        context.Services.AddOutboxMessageService<TContext>(options);

        return context;
    }
}