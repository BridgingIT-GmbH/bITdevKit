// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides registration helpers for outbound HTTP correlation identifier propagation.
/// </summary>
/// <example><code>services.AddCorrelationIdPropagation();</code></example>
public static class CorrelationIdPropagationServiceCollectionExtensions
{
    private const string GlobalRegistrationName = "*";

    /// <summary>
    /// Adds correlation identifier propagation to every client created by
    /// <see cref="IHttpClientFactory"/>.
    /// </summary>
    /// <remarks>
    /// Registration is idempotent. This does not affect manually constructed
    /// <see cref="HttpClient"/> instances.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for continued configuration.</returns>
    /// <example>
    /// <code>
    /// services.AddCorrelationIdPropagation();
    /// services.AddHttpClient&lt;WeatherClient&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCorrelationIdPropagation(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (HasRegistration(services, GlobalRegistrationName))
        {
            return services;
        }

        services.TryAddTransient<CorrelationIdPropagationHandler>();
        services.ConfigureHttpClientDefaults(builder =>
            builder.AddHttpMessageHandler<CorrelationIdPropagationHandler>());
        services.AddSingleton(new CorrelationIdPropagationRegistration(
            GlobalRegistrationName));

        return services;
    }

    /// <summary>
    /// Adds correlation identifier propagation to the named or typed HTTP client.
    /// </summary>
    /// <remarks>
    /// Registration is idempotent for the client name. Calling
    /// <see cref="AddCorrelationIdPropagation(IServiceCollection)"/> already applies the handler to
    /// every client, so an additional per-client registration is unnecessary.
    /// </remarks>
    /// <param name="builder">The HTTP client builder.</param>
    /// <returns>The HTTP client builder for continued configuration.</returns>
    /// <example>
    /// <code>
    /// services.AddHttpClient&lt;WeatherClient&gt;()
    ///     .AddCorrelationIdPropagation();
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddCorrelationIdPropagation(
        this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (HasRegistration(builder.Services, GlobalRegistrationName)
            || HasRegistration(builder.Services, builder.Name))
        {
            return builder;
        }

        builder.Services.TryAddTransient<CorrelationIdPropagationHandler>();
        builder.Services.AddSingleton(new CorrelationIdPropagationRegistration(
            builder.Name));

        return builder.AddHttpMessageHandler<CorrelationIdPropagationHandler>();
    }

    private static bool HasRegistration(
        IServiceCollection services,
        string name) =>
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(CorrelationIdPropagationRegistration)
            && descriptor.ImplementationInstance
                is CorrelationIdPropagationRegistration registration
            && StringComparer.Ordinal.Equals(registration.Name, name));

    private sealed record CorrelationIdPropagationRegistration(string Name);
}
