// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Provides extension methods for registering identity and entity permission services in the DI container.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds entity authorization.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">The delegate used to configure the component.</param>
    /// <param name="configuration">The configuration to apply.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddEntityAuthorization(
        this IServiceCollection services,
        Action<AuthorizationOptionsBuilder> configure, IConfiguration configuration = null)
    {
        var builder = new AuthorizationOptionsBuilder(services, configuration);
        configure(builder);

        return services;
    }
}

/// <summary>
/// Builds authorization options configuration.
/// </summary>
/// <param name="services">The service collection to configure.</param>
/// <param name="configuration">The configuration to apply.</param>
public class AuthorizationOptionsBuilder(IServiceCollection services, IConfiguration configuration = null)
{
    /// <summary>
    /// Gets the services.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;
}
