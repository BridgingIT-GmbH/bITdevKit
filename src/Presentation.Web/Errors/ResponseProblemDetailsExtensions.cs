// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Presentation.Web.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using DevKitOptions = BridgingIT.DevKit.Presentation.Web.Errors.ProblemDetailsOptions;
using NativeOptions = Microsoft.AspNetCore.Http.ProblemDetailsOptions;

/// <summary>Registers and activates DevKit HTTP Problem Details.</summary>
/// <example><code>builder.Services.AddResponseProblemDetails(); app.UseResponseProblemDetails();</code></example>
public static class ResponseProblemDetailsExtensions
{
    private const string IncompatibleRegistrations =
        "AddResponseProblemDetails and the full DevKit AddExceptionHandler registration cannot be used together. Choose one.";

    /// <summary>Registers automatic exception and empty-status handling and shared Result problem preparation.</summary>
    public static IServiceCollection AddResponseProblemDetails(this IServiceCollection services) =>
        services.AddResponseProblemDetails(static _ => { });

    /// <summary>Registers DevKit Problem Details with application overrides.</summary>
    /// <example><code>builder.Services.AddResponseProblemDetails(options => options.IncludeExceptionDetails = builder.Environment.IsDevelopment());</code></example>
    public static IServiceCollection AddResponseProblemDetails(this IServiceCollection services, Action<DevKitOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSharedProblemDetails();
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(CombinedProblemDetailsRegistration)))
        {
            services.TryAddSingleton<CombinedProblemDetailsRegistration>();
            services.AddOptions<DevKitOptions>().ValidateOnStart();
            services.AddSingleton<IValidateOptions<DevKitOptions>, RegistrationValidator>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionHandler, ResponseProblemDetailsExceptionHandler>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<StatusCodePagesOptions>, ResponseProblemDetailsStatusCodeHandler>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, ResponseProblemDetailsHeaderSnapshot>());
        }

        return services.Configure(configure);
    }

    /// <summary>Installs native exception handling followed by native status-code pages.</summary>
    /// <example><code>app.UseResponseProblemDetails();</code></example>
    public static IApplicationBuilder UseResponseProblemDetails(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseExceptionHandler().UseStatusCodePages();
    }

    /// <summary>Registers shared Problem Details preparation without activating automatic exception or status handling.</summary>
    public static void AddSharedProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails();
        if (services.Any(descriptor => descriptor.ImplementationType == typeof(ResponseProblemDetailsService.Preparation)))
        {
            return;
        }

        services.AddOptions<DevKitOptions>();
        services.Replace(ServiceDescriptor.Singleton<IProblemDetailsService, ResponseProblemDetailsService>());
        services.TryAddTransient<IOptionsFactory<NativeOptions>, ResponseProblemDetailsService.Preparation>();
    }

    private sealed class RegistrationValidator(IServiceProvider services) : IValidateOptions<DevKitOptions>
    {
        public ValidateOptionsResult Validate(string name, DevKitOptions options) =>
            services.GetService<StandaloneExceptionHandlerRegistration>() is null
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(IncompatibleRegistrations);
    }
}
