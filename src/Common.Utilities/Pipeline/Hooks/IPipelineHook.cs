// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Observes pipeline lifecycle events for one context type.
/// </summary>
public interface IPipelineHook<TContext>
    where TContext : PipelineContextBase
{
    /// <summary>
    /// Invoked before the pipeline starts processing steps.
    /// </summary>
    ValueTask OnPipelineStartingAsync(
        TContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invoked before an individual step attempt starts.
    /// </summary>
    ValueTask OnStepStartingAsync(
        TContext context,
        IPipelineStepDefinition step,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invoked after an individual step attempt completes.
    /// </summary>
    ValueTask OnStepCompletedAsync(
        TContext context,
        IPipelineStepDefinition step,
        PipelineControl control,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invoked when the pipeline completes successfully.
    /// </summary>
    ValueTask OnPipelineCompletedAsync(
        TContext context,
        Result result,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invoked when the pipeline completes with a failed result.
    /// </summary>
    ValueTask OnPipelineFailedAsync(
        TContext context,
        Result result,
        CancellationToken cancellationToken);
}

/// <summary>
/// Provides a no-op hook base class for selectively overriding pipeline lifecycle events.
/// </summary>
public abstract class PipelineHook<TContext> : IPipelineHook<TContext>
    where TContext : PipelineContextBase
{
    /// <inheritdoc />
    public virtual ValueTask OnPipelineStartingAsync(
        TContext context,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public virtual ValueTask OnStepStartingAsync(
        TContext context,
        IPipelineStepDefinition step,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public virtual ValueTask OnStepCompletedAsync(
        TContext context,
        IPipelineStepDefinition step,
        PipelineControl control,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public virtual ValueTask OnPipelineCompletedAsync(
        TContext context,
        Result result,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public virtual ValueTask OnPipelineFailedAsync(
        TContext context,
        Result result,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
