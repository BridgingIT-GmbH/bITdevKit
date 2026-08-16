// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;

/// <summary>Provides services and application configuration while mapping providers are registered.</summary>
/// <param name="services">The service collection receiving mapping registrations.</param>
/// <param name="configuration">The optional application configuration used by mapping providers.</param>
public class MappingBuilderContext(IServiceCollection services, IConfiguration configuration = null)
{
    /// <summary>Gets the service collection receiving mapping registrations.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>Gets the optional application configuration.</summary>
    public IConfiguration Configuration { get; } = configuration;
}

/// <summary>Provides services and application configuration after Mapster has been registered.</summary>
/// <param name="services">The service collection containing Mapster registrations.</param>
/// <param name="configuration">The optional application configuration used for Mapster settings.</param>
public class MapsterBuilderContext(IServiceCollection services, IConfiguration configuration = null)
{
    /// <summary>Gets the service collection containing Mapster registrations.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>Gets the optional application configuration.</summary>
    public IConfiguration Configuration { get; } = configuration;
}
