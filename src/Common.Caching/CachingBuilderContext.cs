// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides services and configuration while composing DevKit caching providers.
/// </summary>
/// <param name="services">The service collection receiving caching registrations.</param>
/// <param name="configuration">The application configuration, when available.</param>
public class CachingBuilderContext(IServiceCollection services, IConfiguration configuration = null)
{
    /// <summary>
    ///     Gets the service collection receiving caching registrations.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    ///     Gets the application configuration, when available.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;
}
