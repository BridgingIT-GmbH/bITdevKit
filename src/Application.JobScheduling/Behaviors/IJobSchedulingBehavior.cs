// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Represents task.
/// </summary>
/// <returns>The value returned by the delegate.</returns>
public delegate Task JobDelegate();

/// <summary>
/// Defines operations for i job scheduling behavior.
/// </summary>
public interface IJobSchedulingBehavior
{
    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="next">The next used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Execute(IJobExecutionContext context, JobDelegate next);
}
