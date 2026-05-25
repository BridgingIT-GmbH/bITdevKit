// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Defines the public orchestration runtime service surface.
/// </summary>
public interface IOrchestrationService
{
    /// <summary>
    /// Executes an orchestration inline until it reaches a terminal state.
    /// </summary>
    /// <typeparam name="TOrchestration">The orchestration type.</typeparam>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="data">The orchestration input data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<Result<OrchestrationExecuteResult>> ExecuteAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData;

    /// <summary>
    /// Dispatches an orchestration for asynchronous execution.
    /// </summary>
    /// <typeparam name="TOrchestration">The orchestration type.</typeparam>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="data">The orchestration input data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dispatched orchestration instance identifier.</returns>
    Task<Result<Guid>> DispatchAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData;

    /// <summary>
    /// Dispatches an orchestration and waits until the specified condition is met or the timeout expires.
    /// </summary>
    /// <typeparam name="TOrchestration">The orchestration type.</typeparam>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="data">The orchestration input data.</param>
    /// <param name="waitFor">The condition that determines when waiting should stop.</param>
    /// <param name="timeout">The optional wait timeout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The wait result.</returns>
    Task<Result<OrchestrationWaitResult>> DispatchAndWaitAsync<TOrchestration, TData>(
        TData data,
        OrchestrationWaitFor waitFor = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData;

    /// <summary>
    /// Sends a signal to an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="signalName">The signal name.</param>
    /// <param name="payload">The optional signal payload.</param>
    /// <param name="idempotencyKey">The optional idempotency key used to deduplicate signal delivery.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<Result> SignalAsync(
        Guid instanceId,
        string signalName,
        object payload = null,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="reason">The optional pause reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<Result> PauseAsync(
        Guid instanceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<Result> ResumeAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="reason">The optional cancellation reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<Result> CancelAsync(
        Guid instanceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates an orchestration instance immediately.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="reason">The optional termination reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<Result> TerminateAsync(
        Guid instanceId,
        string reason = null,
        CancellationToken cancellationToken = default);
}
