// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Interface representing a module within an application framework.
/// </summary>
public interface IModule
{
    /// <summary>
    ///     Gets or sets a value indicating whether the module is enabled.
    /// </summary>
    /// <remarks>
    ///     This property controls whether the module is active and should be used within the application.
    ///     If set to <c>false</c>, the module will be considered disabled and operations associated with it
    ///     should generally be skipped or result in errors indicating that the module is not enabled.
    /// </remarks>
    bool Enabled { get; set; }

    /// <summary>
    ///     Indicates whether the module has been registered within the system.
    /// </summary>
    bool IsRegistered { get; set; }

    /// <summary>
    ///     Gets the name of the module.
    /// </summary>
    /// <remarks>
    ///     This property provides the unique name identifier for this module.
    /// </remarks>
    string Name { get; }

    /// <summary>
    ///     Gets the priority of the module within the application.
    /// </summary>
    /// <remarks>
    ///     The priority determines the order in which modules are processed or initialized. Modules with lower
    ///     priority numbers are handled before those with higher numbers. This property is particularly useful in
    ///     scenarios where the initialization order of modules affects the application's behavior or dependencies.
    /// </remarks>
    int Priority { get; }

    //IEnumerable<string> Policies => null; // like security/claims/PERMISSIONS

    /// <summary>
    ///     Registers services for the module with the given dependency injection container, configuration, and environment.
    /// </summary>
    /// <param name="services">The collection of services to which the module's services will be added.</param>
    /// <param name="configuration">The configuration settings for the application (optional).</param>
    /// <param name="environment">The hosting environment information (optional).</param>
    /// <returns>The service collection with the module's services added.</returns>
    IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null);

    /// <summary>
    ///     Applies the registered modules to the provided application builder.
    /// </summary>
    /// <param name="app">The application builder to which the modules are to be applied.</param>
    /// <param name="configuration">Optional configuration settings for the modules.</param>
    /// <param name="environment">Optional hosting environment settings for the modules.</param>
    /// <returns>The modified application builder with the modules applied.</returns>
    /// <exception cref="Exception">Thrown if no modules are found. Ensure modules are added with services.AddModules().</exception>
    IApplicationBuilder Use(
        IApplicationBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null);
}