// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;

public class JobSchedulerCustomTriggerTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task CustomTrigger_RegisteredProvider_MaterializesAndExecutesOccurrence()
    {
        var provider = new RecordingCustomTriggerProvider();

        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<EchoJob>("custom-trigger-job", job => job
                .Description("executes an occurrence emitted by a custom trigger provider")
                .WithData<EchoJobData>()
                .AddTrigger("custom", trigger => trigger.Custom<RecordingCustomTriggerProvider>())),
            services => services.AddSingleton(provider));

        var materialized = await harness.MaterializeAsync();
        var occurrences = await harness.GetOccurrencesAsync("custom-trigger-job", "custom");

        materialized.IsSuccess.ShouldBeTrue();
        provider.Calls.ShouldBe(1);
        occurrences.Value.Count.ShouldBe(1);
        occurrences.Value[0].TriggerType.ShouldBe(JobTriggerType.Custom);
        occurrences.Value[0].Properties["provider"].ShouldBe("recording");
        occurrences.Value[0].Data.ShouldBeOfType<EchoJobData>().Message.ShouldBe("from-custom-provider");

        var execution = await harness.ExecuteOccurrenceAsync(occurrences.Value[0].OccurrenceId);

        execution.IsSuccess.ShouldBeTrue();
        execution.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        execution.Value.Messages.ShouldContain("from-custom-provider");
    }

    [Fact]
    public async Task CustomTrigger_SecondMaterialization_DoesNotCreateDuplicateOccurrence()
    {
        var provider = new RecordingCustomTriggerProvider();

        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<EchoJob>("custom-trigger-job", job => job
                .Description("executes an occurrence emitted by a custom trigger provider")
                .WithData<EchoJobData>()
                .AddTrigger("custom", trigger => trigger.Custom<RecordingCustomTriggerProvider>())),
            services => services.AddSingleton(provider));

        var first = await harness.MaterializeAsync();
        var second = await harness.MaterializeAsync();
        var occurrences = await harness.GetOccurrencesAsync("custom-trigger-job", "custom");

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        first.Value.Count.ShouldBe(1);
        second.Value.ShouldBeEmpty();
        provider.Calls.ShouldBe(2);
        occurrences.Value.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CustomTrigger_MissingProvider_FailsClearlyDuringMaterialization()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<EchoJob>("missing-custom-provider", job => job
                .Description("requires a registered custom trigger provider")
                .WithData<EchoJobData>()
                .AddTrigger("custom", trigger => trigger.Custom<RecordingCustomTriggerProvider>())));

        var result = await harness.MaterializeAsync();

        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldContain("is not registered");
    }

    [Fact]
    public async Task CustomTrigger_ProviderFailure_PropagatesFailureMessage()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<EchoJob>("failing-custom-provider", job => job
                .Description("fails when the custom trigger provider returns a failure")
                .WithData<EchoJobData>()
                .AddTrigger("custom", trigger => trigger.Custom<FailingCustomTriggerProvider>())),
            services => services.AddSingleton<FailingCustomTriggerProvider>());

        var result = await harness.MaterializeAsync();

        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldContain("planned custom trigger failure");
    }

    private sealed class RecordingCustomTriggerProvider : IJobCustomTriggerProvider
    {
        public int Calls { get; private set; }

        public Result<JobTriggerEvaluationResult> Materialize(JobTriggerEvaluationContext context)
        {
            this.Calls++;

            if (context.RuntimeState.HasMaterializedOccurrence)
            {
                return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(
                    context.RuntimeState,
                    []));
            }

            var data = new EchoJobData { Message = "from-custom-provider" };

            return Result<JobTriggerEvaluationResult>.Success(new JobTriggerEvaluationResult(
                context.RuntimeState with
                {
                    ActivatedUtc = context.NowUtc,
                    HasMaterializedOccurrence = true,
                    UpdatedDate = context.NowUtc,
                },
                [
                    new JobOccurrenceMaterialization(
                        $"{context.Job.JobName}:{context.Trigger.TriggerName}:custom-provider",
                        context.Job.JobName,
                        context.Trigger.TriggerName,
                        JobTriggerType.Custom,
                        context.NowUtc,
                        null,
                        data,
                        typeof(EchoJobData),
                        new PropertyBag { ["provider"] = "recording" },
                        "custom-provider")
                ]));
        }
    }

    private sealed class FailingCustomTriggerProvider : IJobCustomTriggerProvider
    {
        public Result<JobTriggerEvaluationResult> Materialize(JobTriggerEvaluationContext context)
            => Result<JobTriggerEvaluationResult>.Failure().WithError(new ValidationError("planned custom trigger failure"));
    }
}
