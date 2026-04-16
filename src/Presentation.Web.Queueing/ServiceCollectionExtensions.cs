// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Presentation.Web.Queueing;

/// <summary>
/// Adds operational queueing endpoints to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the queueing endpoints from the fluent queueing builder with explicit options.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="options">The endpoint options to register.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current queueing builder context.</returns>
    public static QueueingBuilderContext AddEndpoints(
        this QueueingBuilderContext context,
        QueueingEndpointsOptions options,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (enabled)
        {
            if (options is not null)
            {
                context.Services.AddSingleton(options);
            }

            context.Services.AddEndpoints<QueueingEndpoints>(enabled);
        }

        return context;
    }

    /// <summary>
    /// Registers the queueing endpoints from the fluent queueing builder with default options.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current queueing builder context.</returns>
    public static QueueingBuilderContext AddEndpoints(this QueueingBuilderContext context, bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (enabled)
        {
            context.Services.AddEndpoints<QueueingEndpoints>(enabled);
        }

        return context;
    }

    /// <summary>
    /// Registers the queueing endpoints with explicit options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The endpoint options to register.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddQueueingEndpoints(new QueueingEndpointsOptions
    /// {
    ///     GroupPath = "/api/_system/queueing",
    ///     GroupTag = "_System.Queueing"
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddQueueingEndpoints(
        this IServiceCollection services,
        QueueingEndpointsOptions options,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (enabled)
        {
            if (options is not null)
            {
                services.AddSingleton(options);
            }

            services.AddEndpoints<QueueingEndpoints>(enabled);
        }

        return services;
    }

    /// <summary>
    /// Registers the queueing endpoints with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    public static IServiceCollection AddQueueingEndpoints(this IServiceCollection services, bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (enabled)
        {
            services.AddEndpoints<QueueingEndpoints>(enabled);
        }

        return services;
    }
}