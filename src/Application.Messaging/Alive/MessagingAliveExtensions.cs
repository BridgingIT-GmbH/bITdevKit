// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides registration helpers for the built-in messaging alive probe.
/// </summary>
/// <example>
/// <code>
/// services.AddMessaging().AliveEnabled(false);
/// </code>
/// </example>
public static class MessagingAliveExtensions
{
    /// <summary>
    /// Enables or disables the built-in messaging alive probe.
    /// </summary>
    /// <param name="context">The messaging builder context.</param>
    /// <param name="enabled">Whether the probe should be available at runtime.</param>
    /// <returns>The current messaging builder context.</returns>
    public static MessagingBuilderContext AliveEnabled(this MessagingBuilderContext context, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = EnsureOptions(context.Services);
        options.Enabled = enabled;

        ServiceCollectionMessagingExtensions.Subscriptions.RemoveAll(item =>
            item.message == typeof(AliveMessage) && item.handler == typeof(AliveMessageHandler));

        if (enabled)
        {
            context.Services.TryAddTransient<AliveMessageHandler>();
            ServiceCollectionMessagingExtensions.Subscriptions.Add((typeof(AliveMessage), typeof(AliveMessageHandler)));
        }

        return context;
    }

    private static MessagingAliveOptions EnsureOptions(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(item => item.ServiceType == typeof(MessagingAliveOptions));
        if (descriptor?.ImplementationInstance is MessagingAliveOptions options)
        {
            return options;
        }

        options = new MessagingAliveOptions();
        services.Replace(ServiceDescriptor.Singleton(options));
        return options;
    }
}
