// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Defines a task that runs during the startup sequence of an application.
///     Implementations should provide the logic of the task within the ExecuteAsync method.
/// </summary>
public interface IStartupTask
{
    /// <summary>
    ///     Executes the startup task asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task ExecuteAsync(CancellationToken cancellationToken);
}