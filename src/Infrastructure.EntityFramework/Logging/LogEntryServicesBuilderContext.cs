// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides fluent configuration for the devkit log entry (database logging) feature registration.
/// </summary>
/// <example>
/// <code>
/// services.AddLogEntryServices&lt;CoreDbContext&gt;()
///     .AddEndpoints(builder.Environment.IsDevelopment());
/// </code>
/// </example>
public class LogEntryServicesBuilderContext(IServiceCollection services, LogEntryMaintenanceServiceOptions options)
{
    /// <summary>
    /// Gets the service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the maintenance options being configured.
    /// </summary>
    public LogEntryMaintenanceServiceOptions Options { get; } = options;
}
