// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Options;

using Microsoft.Extensions.Logging;

/// <summary>
///     Provides configuration options for logging.
///     Inherits from <see cref="OptionsBase" /> to leverage common logging features.
/// </summary>
public class LoggerOptions : OptionsBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoggerOptions" /> class.
    ///     Represents logger options that provide configuration settings for creating loggers.
    /// </summary>
    public LoggerOptions() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="LoggerOptions" /> class.
    ///     Provides configuration options for logging.
    /// </summary>
    public LoggerOptions(ILoggerFactory loggerFactory)
        : base(loggerFactory) { }
}