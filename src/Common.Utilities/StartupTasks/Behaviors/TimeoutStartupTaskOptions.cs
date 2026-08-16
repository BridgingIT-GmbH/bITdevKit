// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Identifies a startup task that supplies timeout options.
/// </summary>
public interface ITimeoutStartupTask
{
    /// <summary>Gets the timeout options for the task.</summary>
    TimeoutStartupTaskOptions Options { get; }
}

/// <summary>
///     Configures the maximum execution duration for a startup task.
/// </summary>
public class TimeoutStartupTaskOptions
{
    /// <summary>Gets or sets the startup-task timeout.</summary>
    public TimeSpan Timeout { get; set; } = new(0, 0, 0, 30);
}
