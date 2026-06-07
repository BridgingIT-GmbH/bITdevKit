// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides registration helpers for the built-in queueing alive probe.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing().AliveEnabled(false);
/// </code>
/// </example>
public static class QueueingAliveExtensions
{
    /// <summary>
    /// Enables or disables the built-in queueing alive probe.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="enabled">Whether the probe should be available at runtime.</param>
    /// <returns>The current queueing builder context.</returns>
    public static QueueingBuilderContext AliveEnabled(this QueueingBuilderContext context, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = EnsureOptions(context.Services);
        options.Enabled = enabled;
        context.RegistrationStore?.Remove(typeof(AliveQueueMessage), typeof(AliveQueueMessageHandler));

        if (enabled)
        {
            context.Services.TryAddTransient<AliveQueueMessageHandler>();
            context.RegistrationStore?.Add(typeof(AliveQueueMessage), typeof(AliveQueueMessageHandler));
        }

        return context;
    }

    private static QueueingAliveOptions EnsureOptions(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(item => item.ServiceType == typeof(QueueingAliveOptions));
        if (descriptor?.ImplementationInstance is QueueingAliveOptions options)
        {
            return options;
        }

        options = new QueueingAliveOptions();
        services.Replace(ServiceDescriptor.Singleton(options));
        return options;
    }
}

