// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

/// <summary>
///     Abstract base class for web modules, providing foundational functionalities.
/// </summary>
/// <remarks>
///     This class extends the <c>ModuleBase</c> and implements <c>IWebModule</c> to configure web-specific
///     functionalities.
/// </remarks>
public abstract class WebModuleBase : ModuleBase, IWebModule
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WebModuleBase" /> class.
    ///     Abstract base class for web modules within the application framework.
    ///     Inherits from <see cref="ModuleBase" /> and implements the <see cref="IWebModule" /> interface.
    /// </summary>
    protected WebModuleBase() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="WebModuleBase" /> class.
    ///     Abstract base class for web modules, inheriting from ModuleBase and implementing the IWebModule interface.
    /// </summary>
    protected WebModuleBase(string name = null, int priority = 99)
        : base(name, priority) { }

    /// <summary>
    ///     Maps endpoint routes for the web module.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used for mapping routes.</param>
    /// <param name="configuration">Optional configuration settings.</param>
    /// <param name="environment">Optional web hosting environment settings.</param>
    /// <returns>The IEndpointRouteBuilder instance with the mapped routes.</returns>
    public virtual IEndpointRouteBuilder Map(
        IEndpointRouteBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        return app;
    }
}