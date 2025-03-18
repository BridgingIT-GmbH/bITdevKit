// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.FileMonitoring;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;

/// <summary>
/// Defines the contract for behaviors that enhance IFileEventProcessor execution.
/// Behaviors execute before and after processing, providing features like logging or retry logic.
/// </summary>
public interface IProcessorBehavior
{
    /// <summary>
    /// Executes before the processor processes the FileEvent.
    /// Allows pre-processing actions (e.g., logging the event details).
    /// </summary>
    /// <param name="context">The processing context containing the FileEvent.</param>
    /// <param name="token">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous pre-processing operation.</returns>
    Task BeforeProcessAsync(ProcessingContext context, CancellationToken token);

    /// <summary>
    /// Executes after the processor processes the FileEvent.
    /// Allows post-processing actions (e.g., logging the result or handling errors).
    /// </summary>
    /// <param name="context">The processing context containing the FileEvent.</param>
    /// <param name="result">The result of the processing operation (success or failure).</param>
    /// <param name="token">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous post-processing operation.</returns>
    Task AfterProcessAsync(ProcessingContext context, Result<bool> result, CancellationToken token);
}