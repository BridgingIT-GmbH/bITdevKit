// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Adapts inline delegate-backed step descriptors to the standard <see cref="IPipelineStep"/> contract.
/// </summary>
/// <param name="definition">The step definition to execute.</param>
/// <param name="serviceProvider">The active scoped service provider.</param>
public class DelegatePipelineStep(
    IPipelineStepDefinition definition,
    IServiceProvider serviceProvider) : IPipelineStep
{
    /// <inheritdoc />
    public string Name => definition.Name;

    /// <inheritdoc />
    public async ValueTask<PipelineControl> ExecuteAsync(
        PipelineContextBase context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var services = new PipelineServiceResolver(serviceProvider);

        if (definition.InlineStep.ContextType == typeof(NullPipelineContext))
        {
            // No-context inline steps use the lightweight non-generic execution wrapper.
            var inlineExecution = new PipelineInlineStepExecution(this.Name, result, options, cancellationToken, services);
            return definition.InlineStep.IsAsync
                ? await ((Func<IPipelineInlineStepExecution, Task<PipelineControl>>)definition.InlineStep.Handler)(inlineExecution)
                : ((Func<IPipelineInlineStepExecution, PipelineControl>)definition.InlineStep.Handler)(inlineExecution);
        }

        // Typed inline steps are activated dynamically from the stored descriptor context type.
        var executionType = typeof(PipelineInlineStepExecution<>).MakeGenericType(definition.InlineStep.ContextType);
        var contextExecution = (IPipelineInlineStepExecution)Activator.CreateInstance(
            executionType,
            this.Name,
            context,
            result,
            options,
            cancellationToken,
            services);

        if (definition.InlineStep.IsAsync)
        {
            return await (Task<PipelineControl>)definition.InlineStep.Handler.DynamicInvoke(contextExecution);
        }

        return (PipelineControl)definition.InlineStep.Handler.DynamicInvoke(contextExecution);
    }
}
