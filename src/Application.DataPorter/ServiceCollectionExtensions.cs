// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.DataPorter;
using Configuration;
using Extensions;

/// <summary>
/// Extension methods for configuring DataPorter services.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DataPorter services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional configuration.</param>
    /// <param name="optionsAction">Optional action to configure the builder context.</param>
    /// <returns>The DataPorter builder context for method chaining.</returns>
    public static DataPorterBuilderContext AddDataPorter(
        this IServiceCollection services,
        IConfiguration configuration = null,
        Action<DataPorterBuilderContext> optionsAction = null)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var context = new DataPorterBuilderContext(services, configuration);
        optionsAction?.Invoke(context);

        // Register core services
        services.TryAddSingleton<IProfileRegistry, ProfileRegistry>();
        services.TryAddSingleton<AttributeConfigurationReader>();
        services.TryAddSingleton<ConfigurationMerger>();
        services.TryAddScoped<DataPorterService>();
        services.TryAddScoped<IDataExporter>(sp => sp.GetRequiredService<DataPorterService>());
        services.TryAddScoped<IDataImporter>(sp => sp.GetRequiredService<DataPorterService>());

        // Register default value converters
        services.TryAddSingleton<IValueConverter, BooleanYesNoConverter>();
        services.TryAddSingleton(typeof(EnumDisplayNameConverter<>));

        return context;
    }

    /// <summary>
    /// Provides extension methods for registering export and import profiles in a DataPorterBuilderContext.
    /// </summary>
    /// <remarks>Use these extension methods to add export and import profiles from assemblies or register
    /// specific profile types for data porting operations. These methods support method chaining for fluent
    /// configuration.</remarks>
    extension(DataPorterBuilderContext context)
    {
        /// <summary>
        /// Registers export profiles from the specified assembly.
        /// </summary>
        /// <typeparam name="T">A type in the assembly to scan.</typeparam>
        /// <returns>The builder context for method chaining.</returns>
        public DataPorterBuilderContext AddProfilesFromAssembly<T>()
        {
            return context.AddProfilesFromAssembly(typeof(T).Assembly);
        }

        /// <summary>
        /// Registers export/import profiles from the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <returns>The builder context for method chaining.</returns>
        public DataPorterBuilderContext AddProfilesFromAssembly(System.Reflection.Assembly assembly)
        {
            EnsureArg.IsNotNull(context, nameof(context));
            EnsureArg.IsNotNull(assembly, nameof(assembly));

            var exportProfileType = typeof(IExportProfile);
            var importProfileType = typeof(IImportProfile);

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (exportProfileType.IsAssignableFrom(type))
                {
                    context.Services.AddSingleton(exportProfileType, type);
                }

                if (importProfileType.IsAssignableFrom(type))
                {
                    context.Services.AddSingleton(importProfileType, type);
                }
            }

            return context;
        }

        /// <summary>
        /// Registers a specific export profile.
        /// </summary>
        /// <typeparam name="TProfile">The profile type.</typeparam>
        /// <returns>The builder context for method chaining.</returns>
        public DataPorterBuilderContext AddExportProfile<TProfile>()
            where TProfile : class, IExportProfile
        {
            EnsureArg.IsNotNull(context, nameof(context));
            context.Services.AddSingleton<IExportProfile, TProfile>();
            return context;
        }

        /// <summary>
        /// Registers a specific import profile.
        /// </summary>
        /// <typeparam name="TProfile">The profile type.</typeparam>
        /// <returns>The builder context for method chaining.</returns>
        public DataPorterBuilderContext AddImportProfile<TProfile>()
            where TProfile : class, IImportProfile
        {
            EnsureArg.IsNotNull(context, nameof(context));
            context.Services.AddSingleton<IImportProfile, TProfile>();
            return context;
        }
    }
}
