// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Invokes pipeline hooks through a non-generic runtime contract.
/// </summary>
public interface IPipelineHookInvoker
{
    /// <summary>
    /// Invokes the hook callback before the pipeline starts processing steps.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    ValueTask OnPipelineStartingAsync(PipelineContextBase context, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes the hook callback before an individual step attempt starts.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="step">The step definition being executed.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    ValueTask OnStepStartingAsync(PipelineContextBase context, IPipelineStepDefinition step, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes the hook callback after an individual step attempt completes.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="step">The step definition that completed.</param>
    /// <param name="control">The control outcome returned by the step execution.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    ValueTask OnStepCompletedAsync(PipelineContextBase context, IPipelineStepDefinition step, PipelineControl control, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes the hook callback when the pipeline completes successfully.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="result">The final pipeline result.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    ValueTask OnPipelineCompletedAsync(PipelineContextBase context, Result result, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes the hook callback when the pipeline completes with a failed result.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="result">The final failed pipeline result.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    ValueTask OnPipelineFailedAsync(PipelineContextBase context, Result result, CancellationToken cancellationToken);
}

/// <summary>
/// Creates runtime invokers for typed pipeline hooks.
/// </summary>
public static class PipelineHookInvoker
{
    /// <summary>
    /// Creates an <see cref="IPipelineHookInvoker"/> for the specified typed hook instance.
    /// </summary>
    /// <param name="hook">The typed pipeline hook instance.</param>
    /// <param name="contextType">The pipeline context type handled by the hook.</param>
    /// <returns>A runtime adapter for the specified hook.</returns>
    public static IPipelineHookInvoker Create(object hook, Type contextType)
    {
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(contextType);

        return (IPipelineHookInvoker)Activator.CreateInstance(
            typeof(PipelineHookInvokerAdapter<>).MakeGenericType(contextType),
            hook);
    }
}

/// <summary>
/// Adapts a typed pipeline hook to the non-generic runtime hook invoker contract.
/// </summary>
/// <typeparam name="TContext">The pipeline context type.</typeparam>
/// <param name="hook">The typed pipeline hook instance.</param>
public class PipelineHookInvokerAdapter<TContext>(IPipelineHook<TContext> hook) : IPipelineHookInvoker
    where TContext : PipelineContextBase
{
    /// <inheritdoc />
    public ValueTask OnPipelineStartingAsync(PipelineContextBase context, CancellationToken cancellationToken)
    {
        return hook.OnPipelineStartingAsync((TContext)context, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask OnStepStartingAsync(PipelineContextBase context, IPipelineStepDefinition step, CancellationToken cancellationToken)
    {
        return hook.OnStepStartingAsync((TContext)context, step, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask OnStepCompletedAsync(PipelineContextBase context, IPipelineStepDefinition step, PipelineControl control, CancellationToken cancellationToken)
    {
        return hook.OnStepCompletedAsync((TContext)context, step, control, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask OnPipelineCompletedAsync(PipelineContextBase context, Result result, CancellationToken cancellationToken)
    {
        return hook.OnPipelineCompletedAsync((TContext)context, result, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask OnPipelineFailedAsync(PipelineContextBase context, Result result, CancellationToken cancellationToken)
    {
        return hook.OnPipelineFailedAsync((TContext)context, result, cancellationToken);
    }
}
