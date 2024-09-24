// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;

/// <summary>
///     Provides a context for configuring and managing startup tasks within an application.
/// </summary>
public class StartupTasksBuilderContext(IServiceCollection services, IConfiguration configuration = null)
{
    /// <summary>
    ///     Gets the service collection to which startup tasks and other services are added.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    ///     Gets the configuration settings used to configure the application.
    /// </summary>
    /// <remarks>
    ///     This property provides access to the application's configuration settings.
    ///     These settings can be used to configure various services and components within the application.
    /// </remarks>
    public IConfiguration Configuration { get; } = configuration;
}