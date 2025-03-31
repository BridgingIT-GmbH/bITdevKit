// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

public class SqlServerOptionsBuilder : OptionsBuilderBase<SqlServerOptions, SqlServerOptionsBuilder>
{
    public SqlServerOptionsBuilder UseConnectionString(string connectionString)
    {
        this.Target.ConnectionString = connectionString;

        return this;
    }

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

    public SqlServerOptionsBuilder UseLogger(bool value = true, bool sensitiveDataLoggingEnabled = false)
    {
        this.Target.LoggerEnabled = value;
        this.Target.SensitiveDataLoggingEnabled = sensitiveDataLoggingEnabled;

        return this;
    }

    public SqlServerOptionsBuilder UseCommandLogger(bool value = true)
    {
        this.Target.CommandLoggerEnabled = value;

        return this;
    }

    public SqlServerOptionsBuilder UseSimpleLogger(bool value = true, LogLevel logLevel = LogLevel.Debug)
    {
        this.Target.SimpleLoggerEnabled = value;
        this.Target.SimpleLoggerLevel = logLevel;
        this.Target.SensitiveDataLoggingEnabled = true;

        return this;
    }

    public SqlServerOptionsBuilder UseIntercepter<TInterceptor>()
        where TInterceptor : class, IInterceptor
    {
        this.Target.InterceptorTypes.Add(typeof(TInterceptor));

        return this;
    }

    public SqlServerOptionsBuilder EnableDetailedErrors(bool value = true)
    {
        this.Target.DetailedErrorsEnabled = value;

        return this;
    }

    public SqlServerOptionsBuilder UseMemoryCache(IMemoryCache cache)
    {
        this.Target.MemoryCache = cache;

        return this;
    }
}