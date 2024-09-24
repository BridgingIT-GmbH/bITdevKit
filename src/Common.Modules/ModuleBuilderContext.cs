// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;

/// <summary>
///     Provides context for building modules with dependencies and configuration.
/// </summary>
public class ModuleBuilderContext(IServiceCollection services, IConfiguration configuration = null)
{
    /// <summary>
    ///     Gets the collection of services that can be used to configure the application's dependencies.
    /// </summary>
    /// <value>
    ///     An instance of <see cref="IServiceCollection" /> which holds the service descriptors.
    /// </value>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    ///     Gets the configuration settings for the module.
    ///     Provides access to application configuration such as settings from appsettings.json or environment variables.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;
}