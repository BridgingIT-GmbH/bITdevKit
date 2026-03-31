// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal interface IPipelineHookInvoker
{
    ValueTask OnPipelineStartingAsync(PipelineContextBase context, CancellationToken cancellationToken);

    ValueTask OnStepStartingAsync(PipelineContextBase context, IPipelineStepDefinition step, CancellationToken cancellationToken);

    ValueTask OnStepCompletedAsync(PipelineContextBase context, IPipelineStepDefinition step, PipelineControl control, CancellationToken cancellationToken);

    ValueTask OnPipelineCompletedAsync(PipelineContextBase context, Result result, CancellationToken cancellationToken);

    ValueTask OnPipelineFailedAsync(PipelineContextBase context, Result result, CancellationToken cancellationToken);
}

internal static class PipelineHookInvoker
{
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
