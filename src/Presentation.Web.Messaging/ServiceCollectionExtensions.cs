// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Presentation.Web.Messaging;

/// <summary>
/// Adds operational messaging endpoints to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the messaging endpoints from the fluent messaging builder with a fluent options builder.
    /// </summary>
    /// <param name="context">The messaging builder context.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current messaging builder context.</returns>
    public static MessagingBuilderContext AddEndpoints(
        this MessagingBuilderContext context,
        Builder<MessagingEndpointsOptionsBuilder, MessagingEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        var options = optionsBuilder?.Invoke(new MessagingEndpointsOptionsBuilder()).Build();

        return context.AddEndpoints(options, enabled);
    }

    /// <summary>
    /// Registers the messaging endpoints from the fluent messaging builder with explicit options.
    /// </summary>
    /// <param name="context">The messaging builder context.</param>
    /// <param name="options">The endpoint options to register.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current messaging builder context.</returns>
    public static MessagingBuilderContext AddEndpoints(
        this MessagingBuilderContext context,
        MessagingEndpointsOptions options,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (enabled)
        {
            if (options is not null)
            {
                context.Services.AddSingleton(options);
            }

            context.Services.AddEndpoints<MessagingEndpoints>(enabled);
        }

        return context;
    }

    /// <summary>
    /// Registers the messaging endpoints from the fluent messaging builder with default options.
    /// </summary>
    /// <param name="context">The messaging builder context.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current messaging builder context.</returns>
    public static MessagingBuilderContext AddEndpoints(this MessagingBuilderContext context, bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (enabled)
        {
            context.Services.AddEndpoints<MessagingEndpoints>(enabled);
        }

        return context;
    }

    /// <summary>
    /// Registers the messaging endpoints with a fluent options builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddMessagingEndpoints(options => options
    ///     .RequireAuthorization()
    ///     .GroupPath("/api/_system/messaging/messages")
    ///     .GroupTag("_System.Messaging"));
    /// </code>
    /// </example>
    public static IServiceCollection AddMessagingEndpoints(
        this IServiceCollection services,
        Builder<MessagingEndpointsOptionsBuilder, MessagingEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = optionsBuilder?.Invoke(new MessagingEndpointsOptionsBuilder()).Build();

        return services.AddMessagingEndpoints(options, enabled);
    }

    /// <summary>
    /// Registers the messaging endpoints with explicit options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The endpoint options to register.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddMessagingEndpoints(options => options
    ///     .GroupPath("/api/_system/messaging/messages")
    ///     .GroupTag("_System.Messaging"));
    /// </code>
    /// </example>
    public static IServiceCollection AddMessagingEndpoints(
        this IServiceCollection services,
        MessagingEndpointsOptions options,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (enabled)
        {
            if (options is not null)
            {
                services.AddSingleton(options);
            }

            services.AddEndpoints<MessagingEndpoints>(enabled);
        }

        return services;
    }

    /// <summary>
    /// Registers the messaging endpoints with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    public static IServiceCollection AddMessagingEndpoints(this IServiceCollection services, bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (enabled)
        {
            services.AddEndpoints<MessagingEndpoints>(enabled);
        }

        return services;
    }
}
