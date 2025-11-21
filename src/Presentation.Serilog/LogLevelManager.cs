// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Serilog.Core;
using Serilog.Events;
using System.Collections.Generic;

public class LogLevelManager(LoggingLevelSwitch levelSwitch)
{
    private readonly LoggingLevelSwitch levelSwitch = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch));

    public LogEventLevel CurrentLevel
    {
        get => this.levelSwitch.MinimumLevel;
        set => this.levelSwitch.MinimumLevel = value;
    }

    public IEnumerable<LogEventLevel> AvailableLevels => Enum.GetValues<LogEventLevel>()
        .Cast<LogEventLevel>()
        .OrderBy(l => (int)l);

    public void SetLevel(string levelName)
    {
        if (!Enum.TryParse<LogEventLevel>(levelName, ignoreCase: true, out var level))
        {
            throw new ArgumentException($"Invalid log level '{levelName}'. Available levels: {string.Join(", ", this.AvailableLevels)}", nameof(levelName));
        }

        this.CurrentLevel = level;
    }

    public void SetLevel(LogEventLevel level)
    {
        this.CurrentLevel = level;
    }

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