// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.Hosting;

using Serilog.Core;

/// <summary>
/// Provides access to the Serilog LoggingLevelSwitch for dynamic log level control.
/// </summary>
public static class LogLevelSwitchProvider
{
    private static LoggingLevelSwitch @switch;

    /// <summary>
    /// Stores the control switch created by Serilog from appsettings.
    /// Call this after Serilog is configured but before using the log level commands.
    /// </summary>
    public static void SetControlSwitch(LoggingLevelSwitch controlSwitch)
    {
        @switch = controlSwitch ?? throw new ArgumentNullException(nameof(controlSwitch));
    }

    /// <summary>
    /// Gets the stored control switch.
    /// </summary>
    public static LoggingLevelSwitch GetControlSwitch()
    {
        if (@switch == null)
        {
            throw new InvalidOperationException(
                "Log level control switch not initialized. Ensure SetControlSwitch() is called after logging configuration.");
        }

        return @switch;
    }
}