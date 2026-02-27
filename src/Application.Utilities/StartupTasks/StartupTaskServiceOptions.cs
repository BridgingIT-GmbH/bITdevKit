// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using Common;

/// <summary>
///     Represents options for configuring the startup tasks hosted service.
/// </summary>
public class StartupTaskServiceOptions : OptionsBase
{
    /// <summary>
    ///     Gets or sets a value indicating whether the application process should be terminated immediately when startup task execution fails.
    /// </summary>
    /// <remarks>
    ///     When enabled, a failure triggers <see cref="Environment.FailFast(string, Exception)" /> after logging.
    /// </remarks>
    public bool HaltOnFailure { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the startup tasks service is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the delay before startup task execution begins.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    ///     Gets or sets the maximum number of startup tasks that may run in parallel.
    /// </summary>
    /// <remarks>
    ///     A value of -1 indicates no explicit limit.
    /// </remarks>
    public int MaxDegreeOfParallelism { get; set; } = -1;
}
