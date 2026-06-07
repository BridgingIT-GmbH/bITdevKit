// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides registration helpers for the built-in jobs alive probe.
/// </summary>
/// <example>
/// <code>
/// services.AddJobScheduler().AliveEnabled(false);
/// </code>
/// </example>
public static class JobAliveExtensions
{
    /// <summary>
    /// Enables or disables the built-in jobs alive probe.
    /// </summary>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="enabled">Whether the probe should be available at runtime.</param>
    /// <returns>The current jobs builder context.</returns>
    public static JobBuilderContext AliveEnabled(this JobBuilderContext context, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = EnsureOptions(context.Services);
        options.Enabled = enabled;
        context.Registrations.Remove(AliveJob.JobName);

        if (enabled)
        {
            context.Services.TryAddTransient<AliveJob>();
            context.WithJob<AliveJob>(AliveJob.JobName, job => job
                .Name("Alive job")
                .Description("Dispatches a built-in diagnostic job that records dashboard activity.")
                .WithConcurrency(10)
                .Group("_bdk")
                .Module("_bdk")
                .AddTrigger(AliveJob.TriggerName, trigger => trigger.Manual()));
        }

        return context;
    }

    private static JobAliveOptions EnsureOptions(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(item => item.ServiceType == typeof(JobAliveOptions));
        if (descriptor?.ImplementationInstance is JobAliveOptions options)
        {
            return options;
        }

        options = new JobAliveOptions();
        services.Replace(ServiceDescriptor.Singleton(options));
        return options;
    }
}
