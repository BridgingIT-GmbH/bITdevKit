// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides registration helpers for the Entity Framework backed orchestration persistence provider.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Entity Framework backed orchestration persistence provider for the current orchestration builder.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IOrchestrationContext"/>.</typeparam>
    /// <param name="context">The orchestration builder context.</param>
    /// <param name="optionsBuilder">The fluent options builder used to customize EF orchestration options.</param>
    /// <returns>The current <see cref="OrchestrationBuilderContext"/> for further composition.</returns>
    public static OrchestrationBuilderContext WithEntityFramework<TContext>(
        this OrchestrationBuilderContext context,
        Builder<EntityFrameworkOrchestrationOptionsBuilder, EntityFrameworkOrchestrationOptions> optionsBuilder = null)
        where TContext : DbContext, IOrchestrationContext
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.TryAddSingleton(sp => CreateOptions(sp, optionsBuilder));
        context.Services.TryAddSingleton(sp =>
            new EntityFrameworkOrchestrationStorageProvider<TContext>(
                sp,
                sp.GetRequiredService<EntityFrameworkOrchestrationOptions>(),
                sp.GetService<IOrchestrationClock>(),
                sp.GetService<ILoggerFactory>()));

        context.Services.Replace(ServiceDescriptor.Singleton<IOrchestrationStorageProvider>(
            sp => sp.GetRequiredService<EntityFrameworkOrchestrationStorageProvider<TContext>>()));
        context.Services.Replace(ServiceDescriptor.Singleton(
            sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Instances));
        context.Services.Replace(ServiceDescriptor.Singleton(
            sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Leases));
        context.Services.Replace(ServiceDescriptor.Singleton(
            sp => sp.GetRequiredService<IOrchestrationStorageProvider>().History));
        context.Services.Replace(ServiceDescriptor.Singleton(
            sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Signals));
        context.Services.Replace(ServiceDescriptor.Singleton(
            sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Timers));
        context.Services.Replace(ServiceDescriptor.Singleton(
            sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Queries));
        context.Services.Replace(ServiceDescriptor.Singleton(
            sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Administration));

        return context;
    }

    private static EntityFrameworkOrchestrationOptions CreateOptions(
        IServiceProvider serviceProvider,
        Builder<EntityFrameworkOrchestrationOptionsBuilder, EntityFrameworkOrchestrationOptions> optionsBuilder)
    {
        var options = optionsBuilder?.Invoke(new EntityFrameworkOrchestrationOptionsBuilder()).Build() ??
            new EntityFrameworkOrchestrationOptions();

        options.Serializer ??= serviceProvider.GetService<ISerializer>() ?? new SystemTextJsonSerializer();

        return options;
    }
}
