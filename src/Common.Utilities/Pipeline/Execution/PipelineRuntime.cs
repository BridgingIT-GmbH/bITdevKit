// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Executes validated pipeline definitions through the unified runtime engine.
/// </summary>
/// <param name="scopeFactory">The scope factory used to create execution scopes.</param>
/// <param name="tracker">The in-memory execution tracker.</param>
/// <param name="loggerFactory">The logger factory used for internal pipeline logging.</param>
public class PipelineRuntime(
    IServiceScopeFactory scopeFactory,
    InMemoryPipelineExecutionTracker tracker,
    ILoggerFactory loggerFactory) : IPipelineRuntime
{
    private readonly ILogger logger = loggerFactory.CreateLogger<PipelineRuntime>();

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
        IPipelineDefinition definition,
        PipelineContextBase context,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);

        using var scope = scopeFactory.CreateScope();
        var normalizedContext = this.NormalizeContext(definition, context);
        var run = await this.ExecuteCoreAsync(
            definition,
            normalizedContext,
            options,
            scope.ServiceProvider,
            GuidGenerator.CreateSequential(),
            backgroundExecution: false,
            cancellationToken);

        return run.Result;
    }

    /// <inheritdoc />
    public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        IPipelineDefinition definition,
        PipelineContextBase context,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);

        var executionId = GuidGenerator.CreateSequential();
        var normalizedContext = this.NormalizeContext(definition, context);

        tracker.MarkAccepted(executionId, definition.Name);

        _ = Task.Run(async () =>
        {
            PipelineCompletion completion = null;

            try
            {
                using (var scope = scopeFactory.CreateScope())
                {
                    var run = await this.ExecuteCoreAsync(
                        definition,
                        normalizedContext,
                        options,
                        scope.ServiceProvider,
                        executionId,
                        backgroundExecution: true,
                        cancellationToken);

                    completion = new PipelineCompletion
                    {
                        ExecutionId = executionId,
                        Status = run.Status,
                        Result = run.Result
                    };
                }

                if (options.CompletionCallback is not null)
                {
                    await this.InvokeCompletionCallbackAsync(definition.Name, completion, options.CompletionCallback);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                tracker.MarkFinished(executionId, normalizedContext, PipelineExecutionStatus.Cancelled, Result.Success());

                if (options.CompletionCallback is not null)
                {
                    await this.InvokeCompletionCallbackAsync(
                        definition.Name,
                        new PipelineCompletion
                        {
                            ExecutionId = executionId,
                            Status = PipelineExecutionStatus.Cancelled,
                            Result = Result.Success()
                        },
                        options.CompletionCallback);
                }
            }
        }, cancellationToken);

        return Task.FromResult(new PipelineExecutionHandle(executionId));
    }

    private async Task<PipelineRunResult> ExecuteCoreAsync(
        IPipelineDefinition definition,
        PipelineContextBase context,
        PipelineExecutionOptions options,
        IServiceProvider serviceProvider,
        Guid executionId,
        bool backgroundExecution,
        CancellationToken cancellationToken)
    {
        var state = new PipelineRunState(executionId, Result.Success());
        var correlationId = Activity.Current?.TraceId.ToString() ?? executionId.ToString("N");

        context.Pipeline.Name = definition.Name;
        context.Pipeline.ExecutionId = executionId;
        context.Pipeline.CorrelationId = correlationId;
        context.Pipeline.StartedUtc = DateTimeOffset.UtcNow;
        context.Pipeline.CompletedUtc = null;
        context.Pipeline.CurrentStepName = null;
        context.Pipeline.ExecutedStepCount = 0;
        context.Pipeline.TotalStepCount = definition.Steps.Count;

        tracker.MarkRunning(executionId, context, state.Result);
        PipelineTypedLogger.LogPipelineStarted(this.logger, PipelineConstants.LogKey, definition.Name, executionId, correlationId);

        var hooks = this.ResolveHooks(serviceProvider, definition);
        var behaviors = this.ResolveBehaviors(serviceProvider, definition);

        // Behaviors wrap the core step loop from outermost to innermost.
        Func<ValueTask<Result>> next = () => this.ExecuteStepsCoreAsync(definition, context, options, serviceProvider, state, hooks, behaviors, cancellationToken);
        foreach (var behavior in behaviors.Reverse<IPipelineBehaviorInvoker>())
        {
            var capturedNext = next;
            next = () => behavior.ExecuteAsync(context, capturedNext, cancellationToken);
        }

        try
        {
            await this.InvokeHooksAsync(hooks, context, static (hook, ctx, ct) => hook.OnPipelineStartingAsync(ctx, ct), cancellationToken);

            state.Result = await next();
            state.Status = state.Result.IsFailure ? PipelineExecutionStatus.Failed : PipelineExecutionStatus.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            state.Status = PipelineExecutionStatus.Cancelled;
            if (!backgroundExecution)
            {
                tracker.MarkFinished(executionId, context, state.Status, state.Result);
                throw;
            }
        }
        catch (Exception ex)
        {
            PipelineTypedLogger.LogPipelineException(this.logger, PipelineConstants.LogKey, definition.Name, executionId, ex);
            state.Result = this.AppendException(state.Result, ex);
            state.Status = PipelineExecutionStatus.Failed;
        }
        finally
        {
            context.Pipeline.CurrentStepName = null;
            context.Pipeline.CompletedUtc = DateTimeOffset.UtcNow;

            if (state.Status == PipelineExecutionStatus.Completed)
            {
                await this.InvokeHooksAsync(hooks, context, (hook, ctx, ct) => hook.OnPipelineCompletedAsync(ctx, state.Result, ct), cancellationToken);
            }
            else if (state.Status == PipelineExecutionStatus.Failed)
            {
                await this.InvokeHooksAsync(hooks, context, (hook, ctx, ct) => hook.OnPipelineFailedAsync(ctx, state.Result, ct), cancellationToken);
            }

            tracker.MarkFinished(executionId, context, state.Status, state.Result);
            PipelineTypedLogger.LogPipelineFinished(this.logger, PipelineConstants.LogKey, definition.Name, executionId, state.Status, context.Pipeline.Duration?.TotalMilliseconds ?? 0);
        }

        return new PipelineRunResult(state.Result, state.Status);
    }

    private async ValueTask<Result> ExecuteStepsCoreAsync(
        IPipelineDefinition definition,
        PipelineContextBase context,
        PipelineExecutionOptions options,
        IServiceProvider serviceProvider,
        PipelineRunState state,
        IReadOnlyList<IPipelineHookInvoker> hooks,
        IReadOnlyList<IPipelineBehaviorInvoker> behaviors,
        CancellationToken cancellationToken)
    {
        var carriedResult = state.Result;

        foreach (var stepDefinition in definition.Steps)
        {
            var step = this.ResolveStep(serviceProvider, stepDefinition);
            var attempts = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // The engine owns the canonical current-step tracking and step-level logging.
                context.Pipeline.CurrentStepName = stepDefinition.Name;
                PipelineTypedLogger.LogStepStarted(this.logger, PipelineConstants.LogKey, definition.Name, stepDefinition.Name, context.Pipeline.ExecutionId);
                await this.InvokeHooksAsync(hooks, context, (hook, ctx, ct) => hook.OnStepStartingAsync(ctx, stepDefinition, ct), cancellationToken);

                var stopwatch = Stopwatch.StartNew();
                var control = await this.ExecuteStepAsync(step, stepDefinition, context, carriedResult, options, behaviors, cancellationToken);
                stopwatch.Stop();

                context.Pipeline.ExecutedStepCount++;
                carriedResult = control.Result;
                state.Result = carriedResult;

                PipelineTypedLogger.LogStepFinished(this.logger, PipelineConstants.LogKey, definition.Name, stepDefinition.Name, context.Pipeline.ExecutionId, control.Outcome, stopwatch.Elapsed.TotalMilliseconds);
                tracker.MarkRunning(context.Pipeline.ExecutionId, context, carriedResult);
                await this.InvokeHooksAsync(hooks, context, (hook, ctx, ct) => hook.OnStepCompletedAsync(ctx, stepDefinition, control, ct), cancellationToken);

                if (control.Outcome == PipelineControlOutcome.Retry)
                {
                    attempts++;
                    // Retry stays on the same step and reuses the returned result/context state.
                    if (attempts <= options.MaxRetryAttemptsPerStep)
                    {
                        PipelineTypedLogger.LogStepRetrying(this.logger, PipelineConstants.LogKey, definition.Name, stepDefinition.Name, context.Pipeline.ExecutionId, attempts, options.MaxRetryAttemptsPerStep, control.Message);
                        continue;
                    }

                    carriedResult = carriedResult.WithError(new Error($"Step '{stepDefinition.Name}' exhausted retry attempts ({options.MaxRetryAttemptsPerStep})."));
                    state.Result = carriedResult;
                    return this.NormalizeFailureResult(carriedResult, options.AccumulateDiagnosticsOnFailure);
                }

                if (carriedResult.IsFailure)
                {
                    if (control.Outcome is PipelineControlOutcome.Break or PipelineControlOutcome.Terminate || !options.ContinueOnFailure)
                    {
                        return this.NormalizeFailureResult(carriedResult, options.AccumulateDiagnosticsOnFailure);
                    }
                }

                switch (control.Outcome)
                {
                    case PipelineControlOutcome.Continue:
                    case PipelineControlOutcome.Skip:
                        break;
                    case PipelineControlOutcome.Break:
                        return this.NormalizeBreakResult(carriedResult, options.AccumulateDiagnosticsOnBreak);
                    case PipelineControlOutcome.Terminate:
                        return carriedResult;
                    default:
                        throw new PipelineDefinitionValidationException($"Unsupported pipeline control outcome '{control.Outcome}'.");
                }

                break;
            }
        }

        return carriedResult;
    }

    private async ValueTask<PipelineControl> ExecuteStepAsync(
        IPipelineStep step,
        IPipelineStepDefinition stepDefinition,
        PipelineContextBase context,
        Result result,
        PipelineExecutionOptions options,
        IReadOnlyList<IPipelineBehaviorInvoker> behaviors,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step behaviors wrap the actual step execution just like pipeline behaviors wrap the full step loop.
            Func<ValueTask<PipelineControl>> next = () => step.ExecuteAsync(context, result, options, cancellationToken);
            foreach (var behavior in behaviors.Reverse<IPipelineBehaviorInvoker>())
            {
                var capturedNext = next;
                next = () => behavior.ExecuteStepAsync(context, stepDefinition, result, capturedNext, cancellationToken);
            }

            return await next();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            PipelineTypedLogger.LogStepException(this.logger, PipelineConstants.LogKey, context.Pipeline.Name, stepDefinition.Name, context.Pipeline.ExecutionId, ex);
            return PipelineControl.Continue(this.AppendException(result, ex));
        }
    }

    private IPipelineStep ResolveStep(IServiceProvider serviceProvider, IPipelineStepDefinition definition)
    {
        if (definition.SourceKind == PipelineStepSourceKind.Type)
        {
            var instance = serviceProvider.GetService(definition.StepType) ?? ActivatorUtilities.CreateInstance(serviceProvider, definition.StepType);
            return (IPipelineStep)instance;
        }

        // Inline definitions are normalized at runtime through the same step contract as class-based steps.
        return new DelegatePipelineStep(definition, serviceProvider);
    }

    private IReadOnlyList<IPipelineHookInvoker> ResolveHooks(IServiceProvider serviceProvider, IPipelineDefinition definition)
    {
        return definition.HookTypes.SafeNull()
            .Select(hookType =>
            {
                var instance = serviceProvider.GetService(hookType) ?? ActivatorUtilities.CreateInstance(serviceProvider, hookType);
                return PipelineHookInvoker.Create(instance, PipelineContextTypeResolver.InferHookContextType(hookType));
            })
            .ToList();
    }

    private IReadOnlyList<IPipelineBehaviorInvoker> ResolveBehaviors(IServiceProvider serviceProvider, IPipelineDefinition definition)
    {
        return definition.BehaviorTypes.SafeNull()
            .Select(behaviorType =>
            {
                var instance = serviceProvider.GetService(behaviorType) ?? ActivatorUtilities.CreateInstance(serviceProvider, behaviorType);
                return PipelineBehaviorInvoker.Create(instance, PipelineContextTypeResolver.InferBehaviorContextType(behaviorType));
            })
            .ToList();
    }

    private async ValueTask InvokeHooksAsync(
        IReadOnlyList<IPipelineHookInvoker> hooks,
        PipelineContextBase context,
        Func<IPipelineHookInvoker, PipelineContextBase, CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken)
    {
        foreach (var hook in hooks.SafeNull())
        {
            try
            {
                await callback(hook, context, cancellationToken);
            }
            catch (Exception ex)
            {
                PipelineTypedLogger.LogHookFailure(this.logger, PipelineConstants.LogKey, context.Pipeline.Name, context.Pipeline.ExecutionId, ex);
            }
        }
    }

    private async Task InvokeCompletionCallbackAsync(
        string pipelineName,
        PipelineCompletion completion,
        Func<PipelineCompletion, ValueTask> callback)
    {
        try
        {
            await callback(completion);
        }
        catch (Exception ex)
        {
            PipelineTypedLogger.LogCompletionCallbackFailed(this.logger, PipelineConstants.LogKey, pipelineName, completion.ExecutionId, ex);
        }
    }

    private PipelineContextBase NormalizeContext(IPipelineDefinition definition, PipelineContextBase context)
    {
        if (definition.ContextType == typeof(NullPipelineContext))
        {
            // No-context pipelines still flow through the unified runtime path via NullPipelineContext.
            return context switch
            {
                null => new NullPipelineContext(),
                NullPipelineContext _ => context,
                _ => throw new PipelineDefinitionValidationException($"Pipeline '{definition.Name}' does not accept a custom execution context.")
            };
        }

        if (context is null)
        {
            throw new PipelineDefinitionValidationException($"Pipeline '{definition.Name}' requires a context of type '{definition.ContextType.PrettyName()}'.");
        }

        if (!definition.ContextType.IsInstanceOfType(context))
        {
            throw new PipelineDefinitionValidationException($"Pipeline '{definition.Name}' expects context '{definition.ContextType.PrettyName()}', not '{context.GetType().PrettyName()}'.");
        }

        return context;
    }

    private Result NormalizeFailureResult(Result result, bool includeDiagnostics)
    {
        return includeDiagnostics ? result : Result.Failure();
    }

    private Result NormalizeBreakResult(Result result, bool includeDiagnostics)
    {
        if (includeDiagnostics)
        {
            return result;
        }

        return result.IsFailure ? Result.Failure() : Result.Success();
    }

    private Result AppendException(Result result, Exception exception)
    {
        return result.WithError(new ExceptionError(exception)).WithMessage(exception.Message);
    }
}
