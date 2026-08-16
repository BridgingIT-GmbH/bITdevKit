// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Builds cosmos options configuration.
/// </summary>
public class CosmosOptionsBuilder : OptionsBuilderBase<CosmosOptions, CosmosOptionsBuilder>
{
    /// <summary>
    /// Configures connection string.
    /// </summary>
    /// <param name="connectionString">The connection string used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosOptionsBuilder UseConnectionString(string connectionString)
    {
        this.Target.ConnectionString = connectionString;

        return this;
    }

    /// <summary>
    /// Configures database.
    /// </summary>
    /// <param name="database">The database used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosOptionsBuilder UseDatabase(string database)
    {
        this.Target.Database = database;

        return this;
    }

    /// <summary>
    /// Configures logger.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <param name="sensitiveDataLoggingEnabled">The sensitive data logging enabled used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosOptionsBuilder UseLogger(bool value = true, bool sensitiveDataLoggingEnabled = false)
    {
        this.Target.LoggerEnabled = value;
        this.Target.SensitiveDataLoggingEnabled = sensitiveDataLoggingEnabled;

        return this;
    }

    /// <summary>
    /// Configures command logger.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosOptionsBuilder UseCommandLogger(bool value = true)
    {
        this.Target.CommandLoggerEnabled = value;

        return this;
    }

    /// <summary>
    /// Configures simple logger.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <param name="logLevel">The log level used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosOptionsBuilder UseSimpleLogger(bool value = true, LogLevel logLevel = LogLevel.Debug)
    {
        this.Target.SimpleLoggerEnabled = value;
        this.Target.SimpleLoggerLevel = logLevel;

        return this;
    }

    /// <summary>
    /// Configures intercepter.
    /// </summary>
    /// <typeparam name="TInterceptor">The interceptor type.</typeparam>
    /// <returns>The result of the operation.</returns>
    public CosmosOptionsBuilder UseIntercepter<TInterceptor>()
        where TInterceptor : class, IInterceptor
    {
        this.Target.InterceptorTypes.Add(typeof(TInterceptor));

        return this;
    }

    /// <summary>
    /// Executes the enable detailed errors operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosOptionsBuilder EnableDetailedErrors(bool value = true)
    {
        this.Target.DetailedErrorsEnabled = value;

        return this;
    }

    /// <summary>
    /// Configures memory cache.
    /// </summary>
    /// <param name="cache">The cache used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosOptionsBuilder UseMemoryCache(IMemoryCache cache)
    {
        this.Target.MemoryCache = cache;

        return this;
    }
}
