// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Serilog;

using BridgingIT.DevKit.Presentation;
using Serilog.Configuration;
using Serilog.Events;

/// <summary>
/// Provides Serilog configuration extensions for the DevKit prompt-aware console sink.
/// </summary>
/// <example>
/// <code>
/// loggerConfiguration.WriteTo.Console();
/// </code>
/// </example>
public static class ConsoleLoggerConfigurationExtensions
{
    /// <summary>
    /// Writes Serilog events to the native console through the DevKit interactive console coordinator.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The Serilog sink configuration.</param>
    /// <param name="outputTemplate">The Serilog output template.</param>
    /// <param name="restrictedToMinimumLevel">The minimum log event level accepted by the sink.</param>
    /// <param name="formatProvider">The optional format provider.</param>
    /// <param name="colorize">A value indicating whether output should be colored by log level.</param>
    /// <returns>The logger configuration for chaining.</returns>
    /// <example>
    /// <code>
    /// loggerConfiguration.WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information);
    /// </code>
    /// </example>
    public static LoggerConfiguration Console(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        string outputTemplate = null,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        IFormatProvider formatProvider = null,
        bool colorize = true)
    {
        ArgumentNullException.ThrowIfNull(loggerSinkConfiguration);

        return loggerSinkConfiguration.Sink(
            new ConsoleSink(outputTemplate, formatProvider, colorize: colorize),
            restrictedToMinimumLevel);
    }
}
