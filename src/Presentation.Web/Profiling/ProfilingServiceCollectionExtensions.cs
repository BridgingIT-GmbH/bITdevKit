// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>Registers profiling Presentation capabilities.</summary>
/// <example><code>services.AddProfiling().AddConsoleCommands();</code></example>
public static class ProfilingServiceCollectionExtensions
{
    /// <summary>Enables the HTTP adapter over explicitly configured Operation Profiling.</summary>
    /// <example><code>services.AddProfiling(o => o.Enabled()).WithOperationProfiling().WithRequestProfiling(o => o.Blacklist("/health/**"));</code></example>
    public static ProfilingBuilderContext WithRequestProfiling(this ProfilingBuilderContext context, Action<RequestProfilingOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = context.Services.FirstOrDefault(d => d.ServiceType == typeof(RequestProfilingOptions))?.ImplementationInstance as RequestProfilingOptions;
        if (options is null)
        {
            options = new RequestProfilingOptions();
            context.Options.Requests.Enabled = true;
            context.Services.AddSingleton(options);
            context.Services.TryAddSingleton<RequestProfilingRuntime>();
            context.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RequestProfilingStartupValidator>());
        }

        configure?.Invoke(new RequestProfilingOptionsBuilder(options, context.Options, context.Services));
        return context;
    }

    /// <summary>Registers the grouped profiling and prof console commands.</summary>
    /// <param name="context">The shared profiling builder.</param>
    /// <param name="enabled">Whether command registration is enabled.</param>
    /// <returns>The same profiling builder.</returns>
    /// <example><code>services.AddProfiling().AddConsoleCommands();</code></example>
    public static ProfilingBuilderContext AddConsoleCommands(
        this ProfilingBuilderContext context,
        bool enabled = true
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!enabled)
        {
            return context;
        }

        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingStatusConsoleCommand>());
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingStartConsoleCommand>());
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingStopConsoleCommand>());
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingSnapshotConsoleCommand>());
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingGarbageCollectionConsoleCommand>());
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingMarkConsoleCommand>());
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingClearConsoleCommand>());
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingAnalyzeConsoleCommand>());
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingExportConsoleCommand>());
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ProfilingImportConsoleCommand>());
        return context;
    }
}
