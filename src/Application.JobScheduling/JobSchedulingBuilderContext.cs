// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Represents job scheduling builder context.
/// </summary>
/// <param name="services">The service collection to configure.</param>
/// <param name="configuration">The configuration to apply.</param>
/// <param name="options">The options controlling the operation.</param>
public class JobSchedulingBuilderContext(
    IServiceCollection services,
    IConfiguration configuration = null,
    JobSchedulingOptions options = null)
{
    /// <summary>
    /// Gets the services.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Gets the options.
    /// </summary>
    public JobSchedulingOptions Options { get; } = options;
}
