// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents the next operation in a startup-task behavior pipeline.</summary>
/// <returns>A task representing completion.</returns>
public delegate Task TaskDelegate();

/// <summary>
///     Defines behavior that executes around a startup task.
/// </summary>
public interface IStartupTaskBehavior
{
    /// <summary>
    ///     Executes behavior logic around the next startup-task delegate.
    /// </summary>
    /// <param name="task">The startup task being executed.</param>
    /// <param name="cancellationToken">A token that can cancel execution.</param>
    /// <param name="next">The next pipeline delegate.</param>
    /// <returns>A task representing completion.</returns>
    Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next);
}
