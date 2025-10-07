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