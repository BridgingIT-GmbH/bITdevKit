// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using Extensions;

public static class ServiceCollectionExtensions
{
    public static CommandBuilderContext WithEntityCommandMessagingBehavior(
        this CommandBuilderContext context,
        EntityCommandMessagingBehaviorOptions options = null)
    {
        var behavior = typeof(EntityCommandMessagingBehavior<,>);

        if (!behavior.ImplementsInterface(typeof(ICommandBehavior<,>)))
        {
            throw new ArgumentException(
                $"Command behavior {behavior.Name} does not implement {nameof(ICommandBehavior)}.");
        }

        context.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), behavior);
        if (options != null)
        {
            context.Services.TryAddSingleton(options);
        }

        return context;
    }

    public static CommandBuilderContext WithEntityCommandMessagingBehavior(
        this CommandBuilderContext context,
        Builder<EntityCommandMessagingBehaviorOptionsBuilder, EntityCommandMessagingBehaviorOptions> optionsBuilder)
    {
        context.WithEntityCommandMessagingBehavior(optionsBuilder(new EntityCommandMessagingBehaviorOptionsBuilder())
            .Build());

        return context;
    }
}