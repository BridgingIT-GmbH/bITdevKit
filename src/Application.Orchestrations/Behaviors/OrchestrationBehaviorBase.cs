// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Provides a base class for orchestration activity behaviors.
/// </summary>
public abstract class OrchestrationBehaviorBase : IOrchestrationBehavior
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationBehaviorBase"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    protected OrchestrationBehaviorBase(ILoggerFactory loggerFactory)
    {
        this.Logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(this.GetType());
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <inheritdoc />
    public abstract Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationActivityExecutionContext context,
        CancellationToken cancellationToken,
        OrchestrationDelegate next);
}
