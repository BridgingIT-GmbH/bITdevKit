// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal interface IPipelineBehaviorInvoker
{
    ValueTask<Result> ExecuteAsync(
        PipelineContextBase context,
        Func<ValueTask<Result>> next,
        CancellationToken cancellationToken);

    ValueTask<PipelineControl> ExecuteStepAsync(
        PipelineContextBase context,
        IPipelineStepDefinition step,
        Result result,
        Func<ValueTask<PipelineControl>> next,
        CancellationToken cancellationToken);
}

internal static class PipelineBehaviorInvoker
{
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
