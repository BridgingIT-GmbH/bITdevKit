// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.Extensions.Caching.Memory;

public class PostgresOptions : OptionsBase
{
    public virtual string ConnectionString { get; set; }

    public virtual bool MigrationsEnabled { get; set; } = true;

    public virtual bool MigrationsSchemaEnabled { get; set; } = true;

    public virtual string MigrationsSchemaName { get; set; }

    public virtual string MigrationsAssemblyName { get; set; }

    public virtual bool LoggerEnabled { get; set; }

    public virtual bool CommandLoggerEnabled { get; set; }

    public virtual bool SimpleLoggerEnabled { get; set; }

    public virtual LogLevel SimpleLoggerLevel { get; set; } = LogLevel.Debug;

    public virtual bool SensitiveDataLoggingEnabled { get; set; }

    public virtual bool DetailedErrorsEnabled { get; set; } = true;

    public virtual IMemoryCache MemoryCache { get; set; }

    public virtual List<Type> InterceptorTypes { get; set; } = [];

    public virtual string SearchPath { get; set; }
}