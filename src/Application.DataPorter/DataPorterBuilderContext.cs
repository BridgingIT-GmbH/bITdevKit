// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder context for configuring DataPorter services.
/// </summary>
public class DataPorterBuilderContext(
    IServiceCollection services,
    IConfiguration configuration = null)
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;
}
