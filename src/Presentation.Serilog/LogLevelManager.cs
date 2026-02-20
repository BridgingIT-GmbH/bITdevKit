// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Serilog.Core;
using Serilog.Events;
using System.Collections.Generic;

/// <summary>
/// Manages the dynamic Serilog logging level at runtime.
/// </summary>
/// <remarks>
/// Wraps a <see cref="LoggingLevelSwitch"/> to read and modify the current minimum log level.
/// Provides helper methods for parsing and describing log levels.
/// </remarks>
/// <param name="levelSwitch">The <see cref="LoggingLevelSwitch"/> controlling Serilog's minimum level.</param>
public class LogLevelManager(LoggingLevelSwitch levelSwitch)
{
    private readonly LoggingLevelSwitch levelSwitch = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch));

    /// <summary>
    /// Gets or sets the current Serilog minimum log level.
    /// </summary>
    public LogEventLevel CurrentLevel
    {
        get => this.levelSwitch.MinimumLevel;
        set => this.levelSwitch.MinimumLevel = value;
    }

    /// <summary>
    /// Gets the ordered list of available <see cref="LogEventLevel"/> values.
    /// </summary>
    public IEnumerable<LogEventLevel> AvailableLevels => Enum.GetValues<LogEventLevel>()
        .Cast<LogEventLevel>()
        .OrderBy(l => (int)l);

    /// <summary>
    /// Sets the current log level using a level name string (case-insensitive).
    /// </summary>
    /// <param name="levelName">The name of the log level (e.g. "Information", "Warning").</param>
    /// <exception cref="ArgumentException">Thrown when the provided name does not match a valid <see cref="LogEventLevel"/>.</exception>
    public void SetLevel(string levelName)
    {
        if (!Enum.TryParse<LogEventLevel>(levelName, ignoreCase: true, out var level))
        {
            throw new ArgumentException($"Invalid log level '{levelName}'. Available levels: {string.Join(", ", this.AvailableLevels)}", nameof(levelName));
        }

        this.CurrentLevel = level;
    }

    /// <summary>
    /// Sets the current log level.
    /// </summary>
    /// <param name="level">The log level to apply.</param>
    public void SetLevel(LogEventLevel level)
    {
        this.CurrentLevel = level;
    }

    /// <summary>
    /// Returns a human-readable description for the specified <see cref="LogEventLevel"/>.
    /// </summary>
    /// <param name="level">The log level for which a description is required.</param>
    /// <returns>A descriptive string for the log level.</returns>
    public string GetLevelDescription(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "Verbose - Most detailed, includes all diagnostics",
            LogEventLevel.Debug => "Debug - Detailed diagnostic information",
            LogEventLevel.Information => "Information - General informational messages",
            LogEventLevel.Warning => "Warning - Warning messages for potential issues",
            LogEventLevel.Error => "Error - Error messages for problems",
            LogEventLevel.Fatal => "Fatal - Fatal errors only",
            _ => level.ToString()
        };
    }
}