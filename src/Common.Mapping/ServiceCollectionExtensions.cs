// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Common;
using Configuration;
using Extensions;
using Mapster;
using MapsterMapper;
using IMapper = BridgingIT.DevKit.Common.IMapper;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds mapping services to the specified service collection and returns a context for further configuration.
    /// </summary>
    /// <remarks>This method is typically called during application startup to register mapping services and
    /// perform additional configuration. The returned context allows chaining further mapping setup
    /// operations.</remarks>
    /// <param name="services">The service collection to which mapping services will be added. Cannot be null.</param>
    /// <param name="configuration">An optional configuration source used to configure mapping services. If null, default configuration is used.</param>
    /// <param name="optionsAction">An optional delegate that can be used to further configure the mapping builder context after initialization.</param>
    /// <returns>A MappingBuilderContext instance that can be used to configure mapping services.</returns>
    public static MappingBuilderContext AddMapping(
        this IServiceCollection services,
        IConfiguration configuration = null,
        Action<MappingBuilderContext> optionsAction = null)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var context = new MappingBuilderContext(services, configuration);
        optionsAction?.Invoke(context);

        return context;
    }

    /// <summary>
    /// Configures Mapster mapping for the specified builder context using the provided configuration and section name.
    /// </summary>
    /// <param name="context">The mapping builder context to configure. Cannot be null.</param>
    /// <param name="configuration">The Mapster configuration to use for mapping. If null, the default configuration is applied.</param>
    /// <param name="section">The configuration section name to use for Mapster settings. Defaults to "Mapping:Mapster" if not specified.</param>
    /// <returns>A MapsterBuilderContext instance configured with Mapster mapping based on the specified parameters.</returns>
    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        EnsureArg.IsNotNull(context, nameof(context));

        return context.WithMapster(
            AppDomain.CurrentDomain.GetAssemblies().SafeGetTypes(typeof(IRegister)), configuration, section);
    }

    public static MapsterBuilderContext WithMapster<T>(
        this MappingBuilderContext context,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
        where T : IRegister
    {
        EnsureArg.IsNotNull(context, nameof(context));

        return context.WithMapster([typeof(T)], configuration, section);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        Type type,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(type, nameof(type));

        return context.WithMapster([type], configuration, section);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        IEnumerable<Type> types,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        EnsureArg.IsNotNull(types, nameof(types));

        return context.WithMapster(types.Select(t => t.Assembly).Distinct(), configuration, section);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        Assembly assembly,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        return context.WithMapster([assembly], configuration, section);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        IEnumerable<Assembly> assemblies,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));
        EnsureArg.IsNotNull(assemblies, nameof(assemblies));

        configuration ??= context.Configuration?.GetSection(section)?.Get<MapsterConfiguration>() ?? new MapsterConfiguration();

        var adapterConfiguration = new TypeAdapterConfig();
        adapterConfiguration.Scan(assemblies.ToArray());

        return context.WithMapster(adapterConfiguration, configuration, section);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        TypeAdapterConfig adapterConfiguration,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));
        EnsureArg.IsNotNull(adapterConfiguration, nameof(adapterConfiguration));

        configuration ??= context.Configuration?.GetSection(section)?.Get<MapsterConfiguration>() ??
            new MapsterConfiguration();

        context.Services.TryAddSingleton(adapterConfiguration);
        context.Services.TryAddScoped<global::MapsterMapper.IMapper, ServiceMapper>(); // https://github.com/MapsterMapper/Mapster/wiki/Dependency-Injection
        context.Services.TryAddSingleton<IMapper, MapsterMapper>();

        return new MapsterBuilderContext(context.Services, context.Configuration);
    }
}

public class MapsterConfiguration;