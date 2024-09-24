// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

/// <summary>
///     Represents a web module within an application framework, extending the IModule interface.
/// </summary>
public interface IWebModule : IModule
{
    /// <summary>
    ///     Maps the specified application routes for the given web module.
    /// </summary>
    /// <param name="app">The endpoint route builder to configure.</param>
    /// <param name="configuration">The configuration settings for the web module.</param>
    /// <param name="environment">The hosting environment information.</param>
    /// <returns>The configured <c>IEndpointRouteBuilder</c> instance.</returns>
    IEndpointRouteBuilder Map(
        IEndpointRouteBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null);
}