// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

public class CosmosOptionsBuilder : OptionsBuilderBase<CosmosOptions, CosmosOptionsBuilder>
{
    public CosmosOptionsBuilder UseConnectionString(string connectionString)
    {
        this.Target.ConnectionString = connectionString;
        return this;
    }

    public CosmosOptionsBuilder UseDatabase(string database)
    {
        this.Target.Database = database;
        return this;
    }

    public CosmosOptionsBuilder UseLogger(bool value = true, bool sensitiveDataLoggingEnabled = false)
    {
        this.Target.LoggerEnabled = value;
        this.Target.SensitiveDataLoggingEnabled = sensitiveDataLoggingEnabled;
        return this;
    }

    public CosmosOptionsBuilder UseCommandLogger(bool value = true)
    {
        this.Target.CommandLoggerEnabled = value;
        return this;
    }

    public CosmosOptionsBuilder UseSimpleLogger(bool value = true, LogLevel logLevel = LogLevel.Debug)
    {
        this.Target.SimpleLoggerEnabled = value;
        this.Target.SimpleLoggerLevel = logLevel;
        return this;
    }

    public CosmosOptionsBuilder UseIntercepter<TInterceptor>()
        where TInterceptor : class, IInterceptor
    {
        this.Target.InterceptorTypes.Add(typeof(TInterceptor));
        return this;
    }

    public CosmosOptionsBuilder EnableDetailedErrors(bool value = true)
    {
        this.Target.DetailedErrorsEnabled = value;
        return this;
    }

    public CosmosOptionsBuilder UseMemoryCache(IMemoryCache cache)
    {
        this.Target.MemoryCache = cache;
        return this;
    }
}