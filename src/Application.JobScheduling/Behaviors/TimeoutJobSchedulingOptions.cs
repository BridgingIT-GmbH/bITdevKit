// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Defines operations for i timeout job scheduling.
/// </summary>
public interface ITimeoutJobScheduling
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    TimeoutJobSchedulingOptions Options { get; }
}

/// <summary>
/// Configures timeout job scheduling.
/// </summary>
public class TimeoutJobSchedulingOptions
{
    /// <summary>
    /// Gets or sets the timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = new(0, 0, 0, 30);
}
