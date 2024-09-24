// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents the options for configuring a startup task.
/// </summary>
public class StartupTaskOptions : OptionsBase
{
    /// <summary>
    ///     Gets or sets a value indicating whether the startup task is enabled.
    /// </summary>
    /// <value>
    ///     A boolean value where <c>true</c> indicates the startup task is enabled, and <c>false</c> indicates it is disabled.
    ///     The default value is <c>true</c>.
    /// </value>
    /// <remarks>
    ///     This property is used to control the execution of startup tasks. By setting it to <c>false</c>, the startup task
    ///     can be skipped.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the delay before the startup task begins execution.
    /// </summary>
    /// <remarks>
    ///     The delay can be specified as a <see cref="TimeSpan" /> value.
    ///     This allows for configurable postponement of the task, useful for tasks that
    ///     should not start immediately when the application launches.
    /// </remarks>
    public TimeSpan StartupDelay { get; set; }

    /// <summary>
    ///     Gets or sets the execution order of the startup task.
    /// </summary>
    /// <remarks>
    ///     A lower value indicates a higher priority, meaning the task
    ///     will be executed earlier during the startup process.
    ///     The default value is 0, which means no particular order.
    /// </remarks>
    public int Order { get; set; }
}