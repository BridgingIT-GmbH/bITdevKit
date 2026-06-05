// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

/// <summary>
/// Identifies the frontend that requested a console command execution.
/// </summary>
/// <example>
/// <code>
/// await executor.ExecuteAsync(line, console, services, ConsoleCommandExecutionSource.Web);
/// </code>
/// </example>
public enum ConsoleCommandExecutionSource
{
    /// <summary>
    /// Command execution was requested by the local terminal frontend.
    /// </summary>
    Terminal,

    /// <summary>
    /// Command execution was requested by the dashboard web console frontend.
    /// </summary>
    Web
}
