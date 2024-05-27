// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Host;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddExceptionHandler(
        this IServiceCollection services)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = new GlobalExceptionHandlerOptions();

        return services.AddExceptionHandler(options);
    }

    public static IServiceCollection AddExceptionHandler(
        this IServiceCollection services,
        Action<GlobalExceptionHandlerOptions> configureOptions)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = new GlobalExceptionHandlerOptions();
        configureOptions?.Invoke(options);

        return services.AddExceptionHandler(options);
    }

    public static IServiceCollection AddExceptionHandler(
        this IServiceCollection services,
        GlobalExceptionHandlerOptions options)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        options ??= new GlobalExceptionHandlerOptions();

        services.AddSingleton(options);

        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<BusinessRuleNotSatisfiedExceptionHandler>();
        services.AddExceptionHandler<SecurityExceptionHandler>();
        services.AddExceptionHandler<ModuleNotEnabledExceptionHandler>();
        services.AddExceptionHandler<AggregateNotFoundExceptionHandler>();
        services.AddExceptionHandler<EntityNotFoundExceptionHandler>();
        services.AddExceptionHandler<NotImplementedExceptionHandler>();
        services.AddExceptionHandler<HttpRequestExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
