// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using Extensions;

/// <summary>
/// Represents service collection extensions.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Executes the with entity command messaging behavior operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the with entity command messaging behavior operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CommandBuilderContext WithEntityCommandMessagingBehavior(
        this CommandBuilderContext context,
        Builder<EntityCommandMessagingBehaviorOptionsBuilder, EntityCommandMessagingBehaviorOptions> optionsBuilder)
    {
        context.WithEntityCommandMessagingBehavior(optionsBuilder(new EntityCommandMessagingBehaviorOptionsBuilder())
            .Build());

        return context;
    }
}
