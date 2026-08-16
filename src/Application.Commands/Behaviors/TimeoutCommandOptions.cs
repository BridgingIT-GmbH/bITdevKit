// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

/// <summary>
/// Defines operations for i timeout command.
/// </summary>
public interface ITimeoutCommand
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    TimeoutCommandOptions Options { get; }
}

/// <summary>
/// Configures timeout command.
/// </summary>
public class TimeoutCommandOptions
{
    /// <summary>
    /// Gets or sets the timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = new(0, 0, 0, 30);
}
