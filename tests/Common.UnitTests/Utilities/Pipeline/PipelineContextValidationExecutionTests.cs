// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Common")]
public class PipelineContextValidationExecutionTests
{
    [Fact]
    public async Task ExecuteAsync_InvalidGeneratedContext_FailsBeforeHooksBehaviorsAndSteps()
    {
        var services = CreateServices();
        var probe = new ValidationProbe();
        services.AddSingleton(probe);
        services.AddPipelines()
            .WithPipeline<ValidatedContextPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<ValidatedContextPipeline, GeneratedValidatedContext>();
        var context = new GeneratedValidatedContext();

        var result = await pipeline.ExecuteAsync(context);

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<FluentValidationError>().Any().ShouldBeTrue();
        result.Messages.ShouldContain("Validation failed");
        probe.PipelineStartingCount.ShouldBe(0);
        probe.PipelineCompletedCount.ShouldBe(0);
        probe.PipelineFailedCount.ShouldBe(0);
        probe.BehaviorCount.ShouldBe(0);
        probe.StepBehaviorCount.ShouldBe(0);
        context.StepExecuted.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ValidGeneratedContext_ExecutesPipelineNormally()
    {
        var services = CreateServices();
        var probe = new ValidationProbe();
        services.AddSingleton(probe);
        services.AddPipelines()
            .WithPipeline<ValidatedContextPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<ValidatedContextPipeline, GeneratedValidatedContext>();
        var context = new GeneratedValidatedContext { SourceFileName = "orders.csv" };

        var result = await pipeline.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        result.Messages.ShouldContain("validated-step");
        probe.PipelineStartingCount.ShouldBe(1);
        probe.PipelineCompletedCount.ShouldBe(1);
        probe.PipelineFailedCount.ShouldBe(0);
        probe.BehaviorCount.ShouldBe(1);
        probe.StepBehaviorCount.ShouldBe(1);
        context.StepExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ManualAndGeneratedValidators_AreBothApplied()
    {
        var services = CreateServices();
        services.AddScoped<IValidator<GeneratedValidatedContext>, AdditionalGeneratedValidatedContextValidator>();
        services.AddPipelines()
            .WithPipeline<ValidatedContextPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<ValidatedContextPipeline, GeneratedValidatedContext>();
        var context = new GeneratedValidatedContext { SourceFileName = "orders.csv" };

        var result = await pipeline.ExecuteAsync(context);

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<FluentValidationError>().Any().ShouldBeTrue();
        result.Messages.ShouldContain("Validation failed");
        context.StepExecuted.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ValidateMethodException_ReturnsFluentValidationError()
    {
        var services = CreateServices();
        services.AddPipelines()
            .WithPipeline<ThrowingValidatedContextPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<ThrowingValidatedContextPipeline, ThrowingValidatedContext>();

        var result = await pipeline.ExecuteAsync(new ThrowingValidatedContext());

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<FluentValidationError>().Any().ShouldBeTrue();
        result.Messages.ShouldContain("Validation failed");
        result.Errors.OfType<FluentValidationError>()
            .SelectMany(static error => error.Errors)
            .Any(static failure => failure.ErrorMessage.Contains("Validation execution failed"))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAndForgetAsync_InvalidGeneratedContext_MarksTrackerAsFailed()
    {
        var services = CreateServices();
        services.AddPipelines()
            .WithPipeline<ValidatedContextPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<ValidatedContextPipeline, GeneratedValidatedContext>();
        var tracker = provider.GetRequiredService<IPipelineExecutionTracker>();

        var handle = await pipeline.ExecuteAndForgetAsync(new GeneratedValidatedContext());

        PipelineExecutionSnapshot snapshot = null;
        for (var i = 0; i < 40; i++)
        {
            snapshot = await tracker.GetAsync(handle.ExecutionId);
            if (snapshot?.Status == PipelineExecutionStatus.Failed)
            {
                break;
            }

            await Task.Delay(25);
        }

        snapshot.ShouldNotBeNull();
        snapshot.Status.ShouldBe(PipelineExecutionStatus.Failed);
        snapshot.Result.IsFailure.ShouldBeTrue();
        snapshot.Result.Errors.OfType<FluentValidationError>().Any().ShouldBeTrue();
    }

    [Fact]
    public void AddPipelines_WithInlinePipeline_RegistersGeneratedContextValidator()
    {
        var services = CreateServices();
        services.AddPipelines()
            .WithPipeline<GeneratedValidatedContext>("validated-inline", builder => builder.AddStep(() => { }));

        var provider = services.BuildServiceProvider();

        provider.GetServices<IValidator<GeneratedValidatedContext>>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddPipelines_WithPackagedPipeline_RegistersGeneratedContextValidator()
    {
        var services = CreateServices();
        services.AddPipelines()
            .WithPipeline<ValidatedContextPipeline>();

        var provider = services.BuildServiceProvider();

        provider.GetServices<IValidator<GeneratedValidatedContext>>().Count().ShouldBe(1);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ValidationProbe>();
        return services;
    }
}

public sealed partial class GeneratedValidatedContext : PipelineContextBase
{
    [ValidateNotEmpty("Source file name is required.")]
    public string SourceFileName { get; set; }

    public bool StepExecuted { get; set; }
}

public sealed partial class ThrowingValidatedContext : PipelineContextBase
{
    [Validate]
    private static void Validate(InlineValidator<ThrowingValidatedContext> validator)
    {
        throw new InvalidOperationException("validator boom");
    }
}

public sealed class ValidatedContextPipeline : PipelineDefinition<GeneratedValidatedContext>
{
    protected override void Configure(IPipelineDefinitionBuilder<GeneratedValidatedContext> builder)
    {
        builder.AddStep<ValidatedContextStep>()
            .AddHook<ValidationProbeHook>()
            .AddBehavior<ValidationProbeBehavior>();
    }
}

public sealed class ThrowingValidatedContextPipeline : PipelineDefinition<ThrowingValidatedContext>
{
    protected override void Configure(IPipelineDefinitionBuilder<ThrowingValidatedContext> builder)
    {
        builder.AddStep(() => { });
    }
}

public sealed class ValidatedContextStep : PipelineStep<GeneratedValidatedContext>
{
    protected override PipelineControl Execute(GeneratedValidatedContext context, Result result, PipelineExecutionOptions options)
    {
        context.StepExecuted = true;
        return PipelineControl.Continue(result.WithMessage("validated-step"));
    }
}

public sealed class ValidationProbe
{
    public int PipelineStartingCount { get; set; }

    public int PipelineCompletedCount { get; set; }

    public int PipelineFailedCount { get; set; }

    public int BehaviorCount { get; set; }

    public int StepBehaviorCount { get; set; }
}

public sealed class ValidationProbeHook(ValidationProbe probe) : PipelineHook<GeneratedValidatedContext>
{
    public override ValueTask OnPipelineStartingAsync(GeneratedValidatedContext context, CancellationToken cancellationToken)
    {
        probe.PipelineStartingCount++;
        return ValueTask.CompletedTask;
    }

    public override ValueTask OnPipelineCompletedAsync(GeneratedValidatedContext context, Result result, CancellationToken cancellationToken)
    {
        probe.PipelineCompletedCount++;
        return ValueTask.CompletedTask;
    }

    public override ValueTask OnPipelineFailedAsync(GeneratedValidatedContext context, Result result, CancellationToken cancellationToken)
    {
        probe.PipelineFailedCount++;
        return ValueTask.CompletedTask;
    }
}

public sealed class ValidationProbeBehavior(ValidationProbe probe) : IPipelineBehavior<GeneratedValidatedContext>
{
    public async ValueTask<Result> ExecuteAsync(GeneratedValidatedContext context, Func<ValueTask<Result>> next, CancellationToken cancellationToken)
    {
        probe.BehaviorCount++;
        return await next();
    }

    public async ValueTask<PipelineControl> ExecuteStepAsync(
        GeneratedValidatedContext context,
        IPipelineStepDefinition step,
        Result result,
        Func<ValueTask<PipelineControl>> next,
        CancellationToken cancellationToken)
    {
        probe.StepBehaviorCount++;
        return await next();
    }
}

public sealed class AdditionalGeneratedValidatedContextValidator : AbstractValidator<GeneratedValidatedContext>
{
    public AdditionalGeneratedValidatedContextValidator()
    {
        this.RuleFor(x => x.SourceFileName).Must(static value => value == "manual-only-ok");
    }
}
