// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Represents job scheduling behavior base.
/// </summary>
public abstract class JobSchedulingBehaviorBase : IJobSchedulingBehavior
{
    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulingBehaviorBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    protected JobSchedulingBehaviorBase(ILoggerFactory loggerFactory)
    {
        this.Logger = this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Executes the execute operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="next">The next used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task Execute(IJobExecutionContext context, JobDelegate next);
}
