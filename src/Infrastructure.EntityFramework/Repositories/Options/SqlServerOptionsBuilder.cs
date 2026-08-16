// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Builds sql server options configuration.
/// </summary>
public class SqlServerOptionsBuilder : OptionsBuilderBase<SqlServerOptions, SqlServerOptionsBuilder>
{
    /// <summary>
    /// Configures connection string.
    /// </summary>
    /// <param name="connectionString">The connection string used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public SqlServerOptionsBuilder UseConnectionString(string connectionString)
    {
        this.Target.ConnectionString = connectionString;

        return this;
    }

    /// <summary>
    /// Configures migrations.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <param name="schemaEnabled">The schema enabled used by the operation.</param>
    /// <param name="schemaName">The schema name used by the operation.</param>
    /// <param name="schemaAssemblyName">The schema assembly name used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public SqlServerOptionsBuilder UseMigrations(
        bool value = true,
        bool schemaEnabled = true,
        string schemaName = null,
        string schemaAssemblyName = null)
    {
        this.Target.MigrationsEnabled = value;
        this.Target.MigrationsSchemaEnabled = schemaEnabled;
        this.Target.MigrationsSchemaName = schemaName;
        this.Target.MigrationsAssemblyName = schemaAssemblyName;

        return this;
    }

    /// <summary>
    /// Configures logger.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <param name="sensitiveDataLoggingEnabled">The sensitive data logging enabled used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public SqlServerOptionsBuilder UseLogger(bool value = true, bool sensitiveDataLoggingEnabled = false)
    {
        this.Target.LoggerEnabled = value;
        this.Target.SensitiveDataLoggingEnabled = sensitiveDataLoggingEnabled;

        return this;
    }

    /// <summary>
    /// Configures idempotent migrations.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public SqlServerOptionsBuilder UseIdempotentMigrations(bool value = true)
    {
        this.Target.IdempotentMigrationsEnabled = value;
        return this;
    }

    /// <summary>
    /// Configures command logger.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public SqlServerOptionsBuilder UseCommandLogger(bool value = true)
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
    public SqlServerOptionsBuilder UseSimpleLogger(bool value = true, LogLevel logLevel = LogLevel.Debug)
    {
        this.Target.SimpleLoggerEnabled = value;
        this.Target.SimpleLoggerLevel = logLevel;
        this.Target.SensitiveDataLoggingEnabled = true;

        return this;
    }

    /// <summary>
    /// Configures intercepter.
    /// </summary>
    /// <typeparam name="TInterceptor">The interceptor type.</typeparam>
    /// <returns>The result of the operation.</returns>
    public SqlServerOptionsBuilder UseIntercepter<TInterceptor>()
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
    public SqlServerOptionsBuilder EnableDetailedErrors(bool value = true)
    {
        this.Target.DetailedErrorsEnabled = value;

        return this;
    }

    /// <summary>
    /// Configures memory cache.
    /// </summary>
    /// <param name="cache">The cache used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public SqlServerOptionsBuilder UseMemoryCache(IMemoryCache cache)
    {
        this.Target.MemoryCache = cache;

        return this;
    }
}
