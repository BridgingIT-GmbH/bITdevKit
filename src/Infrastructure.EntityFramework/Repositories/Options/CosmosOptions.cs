// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Configures cosmos.
/// </summary>
public class CosmosOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public virtual string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the database.
    /// </summary>
    public virtual string Database { get; set; } = "master";

    /// <summary>
    /// Gets or sets the logger enabled.
    /// </summary>
    public virtual bool LoggerEnabled { get; set; }

    /// <summary>
    /// Gets or sets the command logger enabled.
    /// </summary>
    public virtual bool CommandLoggerEnabled { get; set; }

    /// <summary>
    /// Gets or sets the simple logger enabled.
    /// </summary>
    public virtual bool SimpleLoggerEnabled { get; set; }

    /// <summary>
    /// Gets or sets the simple logger level.
    /// </summary>
    public virtual LogLevel SimpleLoggerLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// Gets or sets the sensitive data logging enabled.
    /// </summary>
    public virtual bool SensitiveDataLoggingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the detailed errors enabled.
    /// </summary>
    public virtual bool DetailedErrorsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory cache.
    /// </summary>
    public virtual IMemoryCache MemoryCache { get; set; }

    /// <summary>
    /// Gets or sets the interceptor types.
    /// </summary>
    public virtual List<Type> InterceptorTypes { get; set; } = [];
}
