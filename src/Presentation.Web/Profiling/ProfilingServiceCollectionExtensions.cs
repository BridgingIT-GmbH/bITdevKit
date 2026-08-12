// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Profiling;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers profiling Presentation capabilities.</summary>
/// <example><code>services.AddProfiling().AddConsoleCommands();</code></example>
public static class ProfilingServiceCollectionExtensions
{
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
