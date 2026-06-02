// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.Time.Testing;

public class JobTriggerEvaluationTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public void Materialize_ManualTriggerWithoutDispatchRequest_DoesNotCreateOccurrence()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero));
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(trigger => trigger.Manual());

        var result = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Occurrences.ShouldBeEmpty();
    }

    [Fact]
    public void Materialize_ManualTriggerWithDispatchRequest_CreatesOccurrence()
    {
        var nowUtc = new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(nowUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(trigger => trigger.Manual());

        var result = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest
        {
            ManualDispatchRequested = true,
            DispatchIdentity = "dispatch-001",
        });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Occurrences.Count.ShouldBe(1);
        result.Value.Occurrences[0].DueUtc.ShouldBe(nowUtc);
    }

    [Fact]
    public void Materialize_OneTimeTrigger_MaterializesOnlyOnce()
    {
        var nowUtc = new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero);
        var dueUtc = nowUtc.AddMinutes(10);
        var timeProvider = new FakeTimeProvider(nowUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(trigger => trigger.At(dueUtc));

        var beforeDue = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest());
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        var firstEvaluation = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest { RuntimeState = beforeDue.Value.RuntimeState });
        var secondEvaluation = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest { RuntimeState = firstEvaluation.Value.RuntimeState });

        beforeDue.Value.RuntimeState.DueUtc.ShouldBe(dueUtc);
        beforeDue.Value.Occurrences.ShouldBeEmpty();
        firstEvaluation.Value.Occurrences.Count.ShouldBe(1);
        secondEvaluation.Value.Occurrences.ShouldBeEmpty();
    }

    [Fact]
    public void Materialize_DelayedTrigger_PersistsStableDueUtc()
    {
        var startUtc = new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(startUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(builder => builder.After(TimeSpan.FromMinutes(15)));

        var firstResult = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest());
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        var secondResult = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest { RuntimeState = firstResult.Value.RuntimeState });

        firstResult.Value.RuntimeState.DueUtc.ShouldBe(startUtc.AddMinutes(15));
        secondResult.Value.RuntimeState.DueUtc.ShouldBe(startUtc.AddMinutes(15));
        secondResult.Value.Occurrences.ShouldBeEmpty();
    }

    [Fact]
    public void Materialize_StartupDelayTrigger_CalculatesDueUtcWithoutBlockingStartup()
    {
        var startedUtc = new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(startedUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(builder => builder.StartupDelay(TimeSpan.FromMinutes(2)));

        var result = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest { SchedulerStartedUtc = startedUtc });

        result.IsSuccess.ShouldBeTrue();
        result.Value.RuntimeState.DueUtc.ShouldBe(startedUtc.AddMinutes(2));
        result.Value.Occurrences.ShouldBeEmpty();
    }

    [Fact]
    public void CronEngine_SupportsFivePartAndSixPartExpressions()
    {
        var sut = new CronosJobCronEngine();
        var startUtc = new DateTimeOffset(2026, 05, 26, 09, 02, 10, TimeSpan.Zero);

        var fivePart = sut.GetNextOccurrenceUtc("*/5 * * * *", startUtc, TimeZoneInfo.Utc);
        var sixPart = sut.GetNextOccurrenceUtc("*/30 * * * * *", startUtc, TimeZoneInfo.Utc);

        fivePart.IsSuccess.ShouldBeTrue();
        fivePart.Value.ShouldBe(new DateTimeOffset(2026, 05, 26, 09, 05, 00, TimeSpan.Zero));
        sixPart.IsSuccess.ShouldBeTrue();
        sixPart.Value.ShouldBe(new DateTimeOffset(2026, 05, 26, 09, 02, 30, TimeSpan.Zero));
    }

    [Fact]
    public void CronEngine_InvalidExpression_ReturnsFailure()
    {
        var sut = new CronosJobCronEngine();

        var result = sut.Validate("0 0 0 0 0 0 0");

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void CronEngine_TimeZoneAndDstBehavior_IsExplicit()
    {
        var sut = new CronosJobCronEngine();
        var timeZone = CreateDstTimeZone();
        var fromUtc = new DateTimeOffset(2026, 03, 29, 00, 00, 00, TimeSpan.Zero);

        var result = sut.GetNextOccurrenceUtc("30 2 * * *", fromUtc, timeZone);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new DateTimeOffset(2026, 03, 29, 01, 30, 00, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(JobMissedOccurrencePolicy.Skip, 0)]
    [InlineData(JobMissedOccurrencePolicy.RunOnce, 1)]
    [InlineData(JobMissedOccurrencePolicy.RunAll, 3)]
    public void Materialize_CronTriggerAppliesMissedOccurrencePolicy(JobMissedOccurrencePolicy policy, int expectedCount)
    {
        var nowUtc = new DateTimeOffset(2026, 05, 26, 10, 16, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(nowUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(builder => builder
            .Cron("*/5 * * * *")
            .WithMissedOccurrencePolicy(policy));

        var result = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest
        {
            RuntimeState = new JobTriggerRuntimeState(
                new DateTimeOffset(2026, 05, 26, 10, 00, 00, TimeSpan.Zero),
                null,
                new DateTimeOffset(2026, 05, 26, 10, 00, 00, TimeSpan.Zero),
                false),
        });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Occurrences.Count.ShouldBe(expectedCount);
    }

    [Fact]
    public void Materialize_CronTriggerWithDefaultMissedPolicy_CreatesOneOccurrence()
    {
        var nowUtc = new DateTimeOffset(2026, 05, 26, 10, 16, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(nowUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(builder => builder.Cron("*/5 * * * *"));

        var result = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest
        {
            RuntimeState = new JobTriggerRuntimeState(
                new DateTimeOffset(2026, 05, 26, 10, 00, 00, TimeSpan.Zero),
                null,
                new DateTimeOffset(2026, 05, 26, 10, 00, 00, TimeSpan.Zero),
                false),
        });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Occurrences.Count.ShouldBe(1);
        result.Value.Occurrences[0].ScheduledUtc.ShouldBe(new DateTimeOffset(2026, 05, 26, 10, 15, 00, TimeSpan.Zero));
    }

    [Fact]
    public void Materialize_OneTimeTriggerWithDefaultMissedPolicy_WhenAlreadyPastDue_CreatesOccurrence()
    {
        var nowUtc = new DateTimeOffset(2026, 05, 26, 10, 16, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(nowUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var dueUtc = new DateTimeOffset(2026, 05, 26, 10, 15, 00, TimeSpan.Zero);
        var trigger = CreateTrigger(builder => builder.At(dueUtc));

        var result = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Occurrences.Count.ShouldBe(1);
        result.Value.Occurrences[0].ScheduledUtc.ShouldBe(dueUtc);
    }

    [Fact]
    public void Materialize_CronTriggerCreatesDeterministicOccurrenceKeys()
    {
        var nowUtc = new DateTimeOffset(2026, 05, 26, 10, 16, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(nowUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(builder => builder
            .Cron("*/5 * * * *")
            .WithMissedOccurrencePolicy(JobMissedOccurrencePolicy.RunAll));
        var request = new JobTriggerEvaluationRequest
        {
            RuntimeState = new JobTriggerRuntimeState(
                new DateTimeOffset(2026, 05, 26, 10, 00, 00, TimeSpan.Zero),
                null,
                new DateTimeOffset(2026, 05, 26, 10, 00, 00, TimeSpan.Zero),
                false),
        };

        var first = sut.Materialize(job, trigger, request);
        var second = sut.Materialize(job, trigger, request);

        first.Value.Occurrences.Select(x => x.OccurrenceKey).ShouldBe(second.Value.Occurrences.Select(x => x.OccurrenceKey));
    }

    [Fact]
    public void Materialize_CalendarTrigger_BusinessDaysAndExclusionsMaterializeDeterministically()
    {
        var nowUtc = new DateTimeOffset(2026, 05, 26, 12, 00, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(nowUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(builder => builder
            .Calendar(calendar => calendar
                .At(new TimeOnly(9, 0))
                .OnBusinessDays()
                .ExcludeDates(new DateOnly(2026, 05, 26)))
            .WithMissedOccurrencePolicy(JobMissedOccurrencePolicy.RunAll));

        var result = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest
        {
            RuntimeState = new JobTriggerRuntimeState(
                new DateTimeOffset(2026, 05, 22, 09, 00, 00, TimeSpan.Zero),
                null,
                new DateTimeOffset(2026, 05, 22, 09, 00, 00, TimeSpan.Zero),
                false),
        });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Occurrences.Select(x => x.DueUtc).ShouldBe([new DateTimeOffset(2026, 05, 25, 09, 00, 00, TimeSpan.Zero)]);
    }

    [Theory]
    [InlineData(JobMissedOccurrencePolicy.Skip, 0)]
    [InlineData(JobMissedOccurrencePolicy.RunOnce, 1)]
    [InlineData(JobMissedOccurrencePolicy.RunAll, 2)]
    public void Materialize_CalendarTriggerAppliesMissedOccurrencePolicy(JobMissedOccurrencePolicy policy, int expectedCount)
    {
        var nowUtc = new DateTimeOffset(2026, 06, 01, 12, 00, 00, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(nowUtc);
        var sut = CreateSut(timeProvider);
        var job = CreateJobDefinition();
        var trigger = CreateTrigger(builder => builder
            .Calendar(calendar => calendar
                .At(new TimeOnly(9, 0))
                .LastDayOfMonth())
            .WithMissedOccurrencePolicy(policy));

        var result = sut.Materialize(job, trigger, new JobTriggerEvaluationRequest
        {
            RuntimeState = new JobTriggerRuntimeState(
                new DateTimeOffset(2026, 03, 31, 09, 00, 00, TimeSpan.Zero),
                null,
                new DateTimeOffset(2026, 03, 31, 09, 00, 00, TimeSpan.Zero),
                false),
        });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Occurrences.Count.ShouldBe(expectedCount);
    }

    private static IJobTriggerEvaluator CreateSut(FakeTimeProvider timeProvider)
    {
        return new JobTriggerEvaluator(timeProvider, new CronosJobCronEngine(), new DefaultJobCalendarEngine(), new NullServiceProvider());
    }

    private static JobDefinition CreateJobDefinition()
    {
        return new JobDefinition
        {
            JobName = "cleanup",
            DisplayName = "cleanup-job",
            Description = "Removes stale records.",
            JobType = typeof(JobFoundationRegistrationTests),
            DataType = typeof(Unit),
            Triggers = [],
        };
    }

    private static JobTriggerDefinition CreateTrigger(Action<JobTriggerDefinitionBuilder> configure)
    {
        var builder = new JobTriggerDefinitionBuilder("default", "cleanup", typeof(Unit));
        configure(builder);
        return builder.Build();
    }

    private static TimeZoneInfo CreateDstTimeZone()
    {
        var daylightTransitionStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 1, 0, 0), 3, 5, DayOfWeek.Sunday);
        var daylightTransitionEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 10, 5, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), daylightTransitionStart, daylightTransitionEnd);
        return TimeZoneInfo.CreateCustomTimeZone("JobsTestDst", TimeSpan.Zero, "JobsTestDst", "JobsTestDst", "JobsTestDst-Daylight", [rule]);
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            return null;
        }
    }
}
