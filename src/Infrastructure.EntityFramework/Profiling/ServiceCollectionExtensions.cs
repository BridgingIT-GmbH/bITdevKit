// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Provides Entity Framework persistence for profiling.</summary>
/// <example>
/// <code>
/// services.AddProfiling(options => options.Enabled())
///     .WithEntityFrameworkProvider&lt;AppDbContext&gt;();
/// </code>
/// </example>
public static class ProfilingEntityFrameworkServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default process-local profiling store with a durable Entity Framework store.
    /// </summary>
    /// <typeparam name="TContext">
    /// The application context implementing <see cref="IProfilingDbContext"/>.
    /// </typeparam>
    /// <param name="context">The profiling builder context.</param>
    /// <returns>The same builder context for fluent configuration.</returns>
    /// <remarks>
    /// The context must call <see cref="ProfilingModelBuilderExtensions.ConfigureProfiling"/>
    /// from <c>OnModelCreating</c> so session-owned records use JSON columns.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a different explicit profiling store provider is already registered.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddProfiling(options => options.Enabled())
    ///     .WithEntityFrameworkProvider&lt;AppDbContext&gt;();
    /// </code>
    /// </example>
    public static ProfilingBuilderContext WithEntityFrameworkProvider<TContext>(
        this ProfilingBuilderContext context
    )
        where TContext : DbContext, IProfilingDbContext
    {
        ArgumentNullException.ThrowIfNull(context);

        var providerType = typeof(EntityFrameworkRuntimeProfilingStore<TContext>);
        var registrations = context
            .Services.Where(descriptor => descriptor.ServiceType == typeof(IRuntimeProfilingStore))
            .ToArray();
        if (
            registrations.Any(descriptor =>
                !IsDefaultStore(descriptor) && !IsSameProvider(descriptor, providerType)
            )
        )
        {
            throw new InvalidOperationException(
                "A different profiling store provider is already registered."
            );
        }

        context.Services.RemoveAll<IRuntimeProfilingStore>();
        context.Services.TryAddSingleton<
            IRuntimeProfilingStore,
            EntityFrameworkRuntimeProfilingStore<TContext>
        >();

        return context;
    }

    private static bool IsDefaultStore(ServiceDescriptor descriptor) =>
        descriptor.ImplementationType?.FullName == "BridgingIT.DevKit.Common.InMemoryRuntimeProfilingStore";

    private static bool IsSameProvider(ServiceDescriptor descriptor, Type providerType) =>
        descriptor.ImplementationType == providerType;
}
