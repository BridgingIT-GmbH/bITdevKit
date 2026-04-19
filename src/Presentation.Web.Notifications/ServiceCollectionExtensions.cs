// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Notifications;

/// <summary>
/// Adds operational notification email endpoints to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers notification email endpoints from the fluent notification builder with a fluent options builder.
    /// </summary>
    /// <param name="builder">The notification service builder.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current notification service builder.</returns>
    public static NotificationServiceBuilder AddEndpoints(
        this NotificationServiceBuilder builder,
        Builder<NotificationEmailEndpointsOptionsBuilder, NotificationEmailEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));

        var options = optionsBuilder?.Invoke(new NotificationEmailEndpointsOptionsBuilder()).Build();
        return builder.AddEndpoints(options, enabled);
    }

    /// <summary>
    /// Registers notification email endpoints from the fluent notification builder with explicit options.
    /// </summary>
    /// <param name="builder">The notification service builder.</param>
    /// <param name="options">The endpoint options to register.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current notification service builder.</returns>
    public static NotificationServiceBuilder AddEndpoints(
        this NotificationServiceBuilder builder,
        NotificationEmailEndpointsOptions options,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));

        if (enabled)
        {
            if (options is not null)
            {
                builder.Services.AddSingleton(options);
            }

            builder.Services.AddEndpoints<NotificationEmailEndpoints>(enabled);
        }

        return builder;
    }

    /// <summary>
    /// Registers notification email endpoints from the fluent notification builder with default options.
    /// </summary>
    /// <param name="builder">The notification service builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current notification service builder.</returns>
    public static NotificationServiceBuilder AddEndpoints(this NotificationServiceBuilder builder, bool enabled = true)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));

        if (enabled)
        {
            builder.Services.AddEndpoints<NotificationEmailEndpoints>(enabled);
        }

        return builder;
    }

    /// <summary>
    /// Registers notification email endpoints with a fluent options builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddNotificationEndpoints(options => options
    ///     .RequireAuthorization()
    ///     .GroupPath("/api/_system/notifications/emails")
    ///     .GroupTag("_System.Notifications"));
    /// </code>
    /// </example>
    public static IServiceCollection AddNotificationEndpoints(
        this IServiceCollection services,
        Builder<NotificationEmailEndpointsOptionsBuilder, NotificationEmailEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = optionsBuilder?.Invoke(new NotificationEmailEndpointsOptionsBuilder()).Build();
        return services.AddNotificationEndpoints(options, enabled);
    }

    /// <summary>
    /// Registers notification email endpoints with explicit options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The endpoint options to register.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    public static IServiceCollection AddNotificationEndpoints(
        this IServiceCollection services,
        NotificationEmailEndpointsOptions options,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (enabled)
        {
            if (options is not null)
            {
                services.AddSingleton(options);
            }

            services.AddEndpoints<NotificationEmailEndpoints>(enabled);
        }

        return services;
    }

    /// <summary>
    /// Registers notification email endpoints with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    public static IServiceCollection AddNotificationEndpoints(this IServiceCollection services, bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (enabled)
        {
            services.AddEndpoints<NotificationEmailEndpoints>(enabled);
        }

        return services;
    }
}
