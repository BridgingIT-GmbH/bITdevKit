// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.FileMonitoring;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Context returned by AddFileMonitoring to allow further configuration chaining.
/// </summary>
public class FileMonitoringBuilderContext
{
    private readonly IServiceCollection services;

    internal FileMonitoringBuilderContext(IServiceCollection services) => this.services = services;

    /// <summary>
    /// Provides access to the underlying IServiceCollection for additional service registrations.
    /// </summary>
    public IServiceCollection Services => this.services;
}