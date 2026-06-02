// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

public class JobSchedulerTelemetryTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    private const string TelemetrySourceName = "BridgingIT.DevKit.Application.Jobs";
    private const string TelemetryMeterName = Metrics.MeterName;

    [Fact]
    public async Task DispatchAndWaitEmitsExecutionActivityAndMetrics()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<TelemetryJob>("telemetry-job", job => job
                .Description("telemetry")
                .AddTrigger("manual", trigger => trigger.Manual())));
        using var activities = new ActivityCollector(TelemetrySourceName);
        using var measurements = new MeterCollector(TelemetryMeterName);

        var result = await harness.DispatchAndWaitAsync<TelemetryJob>(options: new JobDispatchOptions { CorrelationId = "corr-123" });
        var occurrence = await harness.FindOccurrenceAsync("telemetry-job", "manual");

        result.IsSuccess.ShouldBeTrue();
        occurrence.ShouldNotBeNull();
        occurrence.CorrelationId.ShouldBe("corr-123");
        activities.Items.ShouldContain(x => x.OperationName == "jobs.management");
        measurements.Items.ShouldContain(x => x.Name == "jobs_executions_started");
        measurements.Items.ShouldContain(x => x.Name == "jobs_executions_completed");
        measurements.Items.ShouldContain(x => x.Name == "jobs_execution_duration");
        measurements.Items.ShouldContain(x => x.Name == "jobs_occurrence_age");
    }

    [Fact]
    public async Task MaterializeDueTriggersEmitsMaterializationTelemetry()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<TelemetryJob>("scheduled-job", job => job
                .Description("scheduled")
                .AddTrigger("once", trigger => trigger.At(new DateTimeOffset(2026, 05, 26, 09, 01, 00, TimeSpan.Zero)))));

        harness.Advance(TimeSpan.FromMinutes(1));

        using var activities = new ActivityCollector(TelemetrySourceName);
        using var measurements = new MeterCollector(TelemetryMeterName);

        var result = await harness.MaterializeDueTriggersAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBeGreaterThan(0);
        activities.Items.ShouldContain(x => x.OperationName == "jobs.trigger.materialize");
        measurements.Items.ShouldContain(x => x.Name == "jobs_occurrences_materialized");
    }

    [Fact]
    public async Task AcceptEventEmitsAcceptanceTelemetry()
    {
        using var harness = this.CreateHarness(_ => { });
        var ingress = harness.Services.GetRequiredService<IJobEventIngress>();
        using var activities = new ActivityCollector(TelemetrySourceName);
        using var measurements = new MeterCollector(TelemetryMeterName);

        var result = await ingress.AcceptAsync("crm.customers", new AcceptedPayload("customer-42"), new JobAcceptedEventOptions
        {
            CorrelationId = "evt-corr",
            SourceId = "customer-42",
        });

        result.IsSuccess.ShouldBeTrue();
        activities.Items.ShouldContain(x =>
            x.OperationName == "jobs.event.accept"
            && Equals(x.GetTagItem("jobs.event.source"), "crm.customers")
            && Equals(x.GetTagItem("jobs.correlation.id"), "evt-corr"));
        measurements.Items.ShouldContain(x => x.Name == "jobs_events_accepted");
    }

    [Fact]
    public async Task EnableJobEmitsManagementTelemetry()
    {
        using var harness = this.CreateHarness(jobs =>
            jobs.WithJob<TelemetryJob>("managed-job", job => job
                .Description("managed")
                .AddTrigger("manual", trigger => trigger.Manual())));

        (await harness.Scheduler.DisableJobAsync("managed-job")).IsSuccess.ShouldBeTrue();

        using var activities = new ActivityCollector(TelemetrySourceName);
        using var measurements = new MeterCollector(TelemetryMeterName);

        var result = await harness.Scheduler.EnableJobAsync("managed-job");

        result.IsSuccess.ShouldBeTrue();
        activities.Items.ShouldContain(x =>
            x.OperationName == "jobs.management"
            && Equals(x.GetTagItem("jobs.operation"), "enable-job")
            && Equals(x.GetTagItem("jobs.job.name"), "managed-job"));
        measurements.Items.Any(x =>
            x.Name == "jobs_management_operations"
            && x.Tags.TryGetValue("jobs.operation", out var operation)
            && Equals(operation, "enable-job")).ShouldBeTrue();
    }

    private sealed record AcceptedPayload(string CustomerId);

    private sealed class TelemetryJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
        {
            context.Messages.Add("ok");
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class ActivityCollector : IDisposable
    {
        private readonly ActivityListener listener;

        public ActivityCollector(string sourceName)
        {
            this.listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == sourceName,
                SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => this.Items.Add(activity),
            };

            ActivitySource.AddActivityListener(this.listener);
        }

        public List<Activity> Items { get; } = [];

        public void Dispose()
        {
            this.listener.Dispose();
        }
    }

    private sealed class MeterCollector : IDisposable
    {
        private readonly MeterListener listener;

        public MeterCollector(string meterName)
        {
            this.listener = new MeterListener();
            this.listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            this.listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => this.Items.Add(new MeasurementRecord(instrument.Name, value, ToDictionary(tags))));
            this.listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => this.Items.Add(new MeasurementRecord(instrument.Name, value, ToDictionary(tags))));
            this.listener.Start();
        }

        public List<MeasurementRecord> Items { get; } = [];

        public void Dispose()
        {
            this.listener.Dispose();
        }

        private static Dictionary<string, object> ToDictionary(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                values[tag.Key] = tag.Value;
            }

            return values;
        }
    }

    private sealed record MeasurementRecord(string Name, object Value, IReadOnlyDictionary<string, object> Tags);
}
