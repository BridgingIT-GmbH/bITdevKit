// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides registration helpers for the built-in orchestration alive probe.
/// </summary>
/// <example>
/// <code>
/// services.AddOrchestrations().AliveEnabled(false);
/// </code>
/// </example>
public static class OrchestrationAliveExtensions
{
    /// <summary>
    /// Enables or disables the built-in orchestration alive probe.
    /// </summary>
    /// <param name="context">The orchestration builder context.</param>
    /// <param name="enabled">Whether the probe should be available at runtime.</param>
    /// <returns>The current orchestration builder context.</returns>
    public static OrchestrationBuilderContext AliveEnabled(this OrchestrationBuilderContext context, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = EnsureOptions(context.Services);
        options.Enabled = enabled;

        var store = context.Services.FirstOrDefault(item => item.ServiceType == typeof(OrchestrationRegistrationStore))?.ImplementationInstance as OrchestrationRegistrationStore;
        store?.Remove(typeof(AliveOrchestration));

        if (enabled)
        {
            context.Services.TryAddTransient<AliveOrchestration>();
            store?.Add(typeof(AliveOrchestration));
            store?.RegisterName("alive-orchestration", typeof(AliveOrchestration));
        }

        return context;
    }

    private static OrchestrationAliveOptions EnsureOptions(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(item => item.ServiceType == typeof(OrchestrationAliveOptions));
        if (descriptor?.ImplementationInstance is OrchestrationAliveOptions options)
        {
            return options;
        }

        options = new OrchestrationAliveOptions();
        services.Replace(ServiceDescriptor.Singleton(options));
        return options;
    }
}

