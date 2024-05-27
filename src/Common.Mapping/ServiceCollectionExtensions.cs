// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using AutoMapper;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    public static AutoMapperBuilderContext WithAutoMapper(
        this MappingBuilderContext context,
        AutoMapperConfiguration configuration = null,
        string section = "Mapping:AutoMapper")
    {
        EnsureArg.IsNotNull(context, nameof(context));

        return context.WithAutoMapper(
            BridgingIT.DevKit.Common.AssemblyExtensions
                .SafeGetTypes(AppDomain.CurrentDomain.GetAssemblies(), typeof(Profile)).Select(t => t.Assembly).Distinct(),
            configuration,
            section);
    }

    public static AutoMapperBuilderContext WithAutoMapper<T>(
        this MappingBuilderContext context,
        AutoMapperConfiguration configuration = null,
        string section = "Mapping:AutoMapper")
        where T : AutoMapper.Profile
    {
        EnsureArg.IsNotNull(context, nameof(context));

        return context.WithAutoMapper(typeof(T), configuration, section);
    }

    public static AutoMapperBuilderContext WithAutoMapper(
        this MappingBuilderContext context,
        Type type,
        AutoMapperConfiguration configuration = null,
        string section = "Mapping:AutoMapper")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(type, nameof(type));

        return context.WithAutoMapper(new[] { type }, configuration, section);
    }

    public static AutoMapperBuilderContext WithAutoMapper(
        this MappingBuilderContext context,
        IEnumerable<Type> types,
        AutoMapperConfiguration configuration = null,
        string section = "Mapping:AutoMapper")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(types, nameof(types));

        return context.WithAutoMapper(types.Select(t => t.Assembly).Distinct(), configuration, section);
    }

    public static AutoMapperBuilderContext WithAutoMapper(
        this MappingBuilderContext context,
        Assembly assembly,
        AutoMapperConfiguration configuration = null,
        string section = "Mapping:AutoMapper")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(assembly, nameof(assembly));

        return context.WithAutoMapper(new[] { assembly }, configuration, section);
    }

    public static AutoMapperBuilderContext WithAutoMapper(
        this MappingBuilderContext context,
        IEnumerable<Assembly> assemblies,
        AutoMapperConfiguration configuration = null,
        string section = "Mapping:AutoMapper")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));
        EnsureArg.IsNotNull(assemblies, nameof(assemblies));

        configuration ??= context.Configuration?.GetSection(section)?.Get<AutoMapperConfiguration>() ?? new AutoMapperConfiguration();

        context.Services.AddAutoMapper(assemblies);
        context.Services.TryAddSingleton<BridgingIT.DevKit.Common.IMapper, BridgingIT.DevKit.Common.AutoMapper>();

        return new AutoMapperBuilderContext(context.Services, context.Configuration);
    }

    public static AutoMapperBuilderContext WithAutoMapper(
        this MappingBuilderContext context,
        Action<IMapperConfigurationExpression> configAction,
        AutoMapperConfiguration configuration = null,
        string section = "Mapping:AutoMapper")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<AutoMapperConfiguration>() ?? new AutoMapperConfiguration();

        context.Services.AddAutoMapper(configAction);
        context.Services.TryAddSingleton<BridgingIT.DevKit.Common.IMapper, BridgingIT.DevKit.Common.AutoMapper>();

        return new AutoMapperBuilderContext(context.Services, context.Configuration);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        EnsureArg.IsNotNull(context, nameof(context));

        return context.WithMapster(
            BridgingIT.DevKit.Common.AssemblyExtensions.SafeGetTypes(AppDomain.CurrentDomain.GetAssemblies(), typeof(IRegister)),
            configuration,
            section);
    }

    public static MapsterBuilderContext WithMapster<T>(
        this MappingBuilderContext context,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
        where T : Mapster.IRegister
    {
        EnsureArg.IsNotNull(context, nameof(context));

        return context.WithMapster(new[] { typeof(T) }, configuration, section);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        Type type,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(type, nameof(type));

        return context.WithMapster(new[] { type }, configuration, section);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        IEnumerable<Type> types,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        EnsureArg.IsNotNull(types, nameof(types));

        return context.WithMapster(
            types.Select(t => t.Assembly).Distinct(), configuration, section);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        Assembly assembly,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        return context.WithMapster(new[] { assembly }, configuration, section);
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

        var adapterConfiguration = new Mapster.TypeAdapterConfig();
        adapterConfiguration.Scan(assemblies.ToArray());

        return context.WithMapster(adapterConfiguration, configuration, section);
    }

    public static MapsterBuilderContext WithMapster(
        this MappingBuilderContext context,
        Mapster.TypeAdapterConfig adapterConfiguration,
        MapsterConfiguration configuration = null,
        string section = "Mapping:Mapster")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));
        EnsureArg.IsNotNull(adapterConfiguration, nameof(adapterConfiguration));

        configuration ??= context.Configuration?.GetSection(section)?.Get<MapsterConfiguration>() ?? new MapsterConfiguration();

        context.Services.TryAddSingleton(adapterConfiguration);
        context.Services.TryAddScoped<MapsterMapper.IMapper, MapsterMapper.ServiceMapper>(); // https://github.com/MapsterMapper/Mapster/wiki/Dependency-Injection
        context.Services.TryAddSingleton<BridgingIT.DevKit.Common.IMapper, BridgingIT.DevKit.Common.MapsterMapper>();

        return new MapsterBuilderContext(context.Services, context.Configuration);
    }
}

public class AutoMapperConfiguration
{
}

public class MapsterConfiguration
{
}