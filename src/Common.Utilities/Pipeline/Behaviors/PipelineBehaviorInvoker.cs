// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Invokes pipeline behaviors through a non-generic runtime contract.
/// </summary>
public interface IPipelineBehaviorInvoker
{
    /// <summary>
    /// Executes behavior logic around the full pipeline invocation.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="next">The inner pipeline delegate.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The final pipeline result returned by the inner delegate.</returns>
    ValueTask<Result> ExecuteAsync(
        PipelineContextBase context,
        Func<ValueTask<Result>> next,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes behavior logic around one pipeline step invocation.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="step">The step definition being executed.</param>
    /// <param name="result">The carried pipeline result before the step runs.</param>
    /// <param name="next">The inner step delegate.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The <see cref="PipelineControl"/> returned by the inner step delegate.</returns>
    ValueTask<PipelineControl> ExecuteStepAsync(
        PipelineContextBase context,
        IPipelineStepDefinition step,
        Result result,
        Func<ValueTask<PipelineControl>> next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Creates runtime invokers for typed pipeline behaviors.
/// </summary>
public static class PipelineBehaviorInvoker
{
    /// <summary>
    /// Creates an <see cref="IPipelineBehaviorInvoker"/> for the specified typed behavior instance.
    /// </summary>
    /// <param name="behavior">The typed pipeline behavior instance.</param>
    /// <param name="contextType">The pipeline context type handled by the behavior.</param>
    /// <returns>A runtime adapter for the specified behavior.</returns>
    public static IPipelineBehaviorInvoker Create(object behavior, Type contextType)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        ArgumentNullException.ThrowIfNull(contextType);

        return (IPipelineBehaviorInvoker)Activator.CreateInstance(
            typeof(PipelineBehaviorInvokerAdapter<>).MakeGenericType(contextType),
            behavior);
    }
}

/// <summary>
/// Adapts a typed pipeline behavior to the non-generic runtime behavior invoker contract.
/// </summary>
/// <typeparam name="TContext">The pipeline context type.</typeparam>
/// <param name="behavior">The typed pipeline behavior instance.</param>
public class PipelineBehaviorInvokerAdapter<TContext>(IPipelineBehavior<TContext> behavior) : IPipelineBehaviorInvoker
    where TContext : PipelineContextBase
{
    /// <inheritdoc />
    public ValueTask<Result> ExecuteAsync(
        PipelineContextBase context,
        Func<ValueTask<Result>> next,
        CancellationToken cancellationToken)
    {
        return behavior.ExecuteAsync((TContext)context, next, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<PipelineControl> ExecuteStepAsync(
        PipelineContextBase context,
        IPipelineStepDefinition step,
        Result result,
        Func<ValueTask<PipelineControl>> next,
        CancellationToken cancellationToken)
    {
        return behavior.ExecuteStepAsync((TContext)context, step, result, next, cancellationToken);
    }
}
