// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides registration helpers for the Entity Framework backed jobs persistence provider.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Entity Framework backed jobs persistence provider for the current jobs builder.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IJobsContext"/>.</typeparam>
    /// <param name="context">The jobs builder context.</param>
    /// <param name="optionsBuilder">The fluent options builder used to customize EF jobs options.</param>
    /// <returns>The current <see cref="JobBuilderContext"/> for further composition.</returns>
    public static JobBuilderContext WithEntityFramework<TContext>(
        this JobBuilderContext context,
        Builder<EntityFrameworkJobsOptionsBuilder, EntityFrameworkJobsOptions> optionsBuilder = null)
        where TContext : DbContext, IJobsContext
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.TryAddSingleton(sp => CreateOptions(sp, optionsBuilder));
        context.Services.TryAddSingleton(sp =>
            new EntityFrameworkJobStoreProvider<TContext>(
                sp,
                sp.GetRequiredService<EntityFrameworkJobsOptions>(),
                sp.GetService<TimeProvider>(),
                sp.GetService<ILoggerFactory>()));

        context.Services.Replace(ServiceDescriptor.Singleton<IJobStoreProvider>(
            sp => sp.GetRequiredService<EntityFrameworkJobStoreProvider<TContext>>()));

        return context;
    }

    private static EntityFrameworkJobsOptions CreateOptions(
        IServiceProvider serviceProvider,
        Builder<EntityFrameworkJobsOptionsBuilder, EntityFrameworkJobsOptions> optionsBuilder)
    {
        var options = optionsBuilder?.Invoke(new EntityFrameworkJobsOptionsBuilder()).Build() ??
            new EntityFrameworkJobsOptions();

        options.Serializer ??= serviceProvider.GetService<ISerializer>() ?? new SystemTextJsonSerializer();

        return options;
    }
}