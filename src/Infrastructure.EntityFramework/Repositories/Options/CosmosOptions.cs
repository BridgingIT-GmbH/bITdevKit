// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

public class CosmosOptions : OptionsBase
{
    public virtual string ConnectionString { get; set; }

    public virtual string Database { get; set; } = "master";

    public virtual bool LoggerEnabled { get; set; }

    public virtual bool CommandLoggerEnabled { get; set; }

    public virtual bool SimpleLoggerEnabled { get; set; }

    public virtual LogLevel SimpleLoggerLevel { get; set; } = LogLevel.Debug;

    public virtual bool SensitiveDataLoggingEnabled { get; set; }

    public virtual bool DetailedErrorsEnabled { get; set; } = true;

    public virtual IMemoryCache MemoryCache { get; set; }

    public virtual List<Type> InterceptorTypes { get; set; } = new List<Type>();
}