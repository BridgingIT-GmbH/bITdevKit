// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Tests.Profiling;

using BridgingIT.DevKit.Common;
using Shouldly;
using Xunit;

/// <summary>Checks identical bounded analysis and correlation contracts on every storage engine.</summary>
public abstract partial class ProfilingStorageContractTestsBase
{
    /// <summary>Checks retained-history selection and correlation boundaries against the actual provider.</summary>
    [Fact]
    public async Task ReverseOverlap_UsesHalfOpenIntervals_AndIncludesExactZeroDurationInstants()
    {
        var h = await this.CreateAsync();
        var from = h.Clock.GetUtcNow();
        var ended = h.Record();
        var instant = h.Record(durationMs: 0);
        var upper = instant with { Id = Guid.NewGuid(), StartedUtc = from.AddSeconds(1), CompletedUtc = from.AddSeconds(1) };
        (await h.Store.AppendAsync([h.Envelope(ended, 1), h.Envelope(instant, 2), h.Envelope(upper, 3)])).IsSuccess.ShouldBeTrue();
        var result = await h.Store.QueryAsync(h.Query() with { IntervalOverlap = true, FromUtc = from, ToUtc = from.AddSeconds(1) });
        result.IsSuccess.ShouldBeTrue();
        result.Value.Records.Single().Id.ShouldBe(instant.Id);
    }

    /// <summary>Checks retained-history selection and correlation boundaries against the actual provider.</summary>
    [Fact]
    public async Task AnalysisService_UsesRetainedRoots_WithoutPublishingPendingWork()
    {
        var options = new ProfilingOptions { Enabled = true };
        var h = await this.CreateAsync(options);
        foreach (var (duration, sequence) in new[] { (10, 1), (100, 2), (20, 3), (200, 4) })
        { (await h.Store.AppendAsync([h.Envelope(h.Record(durationMs: duration), sequence)])).IsSuccess.ShouldBeTrue(); }

        var sut = new OperationProfilingQueryService(h.Provider, options, null, clock: h.Clock);
        var analysis = await sut.AnalyzeAsync(h.Query());
        analysis.IsSuccess.ShouldBeTrue();
        analysis.Value.Count.ShouldBe(4);
        analysis.Value.P50Milliseconds.ShouldBe(60);
        analysis.Value.P95Milliseconds.ShouldBe(200);
        (await sut.AnalyzeAsync(h.Query() with { MaximumAnalysisCount = 3 })).Errors.ShouldContain(error => error is ProfilingQueryLimitError);
        (await sut.GroupAsync(h.Query())).Value.Groups.Single().Count.ShouldBe(4);
    }

    /// <summary>Checks retained-history selection and correlation boundaries against the actual provider.</summary>
    [Fact]
    public async Task RuntimeCorrelation_UsesExactProcessAndObservedWindow_WithSurroundingLabels()
    {
        var h = await this.CreateAsync();
        (await h.Store.AppendAsync([h.Envelope(h.Record(), 1)])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);
        var started = h.Clock.GetUtcNow().AddSeconds(-10);
        var runtime = h.Provider.Runtime;
        var session = (await runtime.GetOrCreateActiveSessionAsync(new(ProfilingIdentityFactory.CreateRuntimeSession(), "correlation", started,
            RuntimeProfilingOptions.MinimumSamplingInterval, TimeSpan.FromMinutes(1), []))).Value.Session;
        var participation = new RuntimeProfilingNodeParticipation
        {
            SessionId = session.Identity.Id, SessionKey = session.Identity.Key, NodeId = h.Node.Identity.Id, NodeKey = h.Node.Identity.Key,
            JoinedUtc = started, State = RuntimeProfilingParticipationState.Collecting, Role = RuntimeProfilingNodeRole.AdHocContributor,
        };
        (await runtime.UpsertParticipationAsync(participation)).IsSuccess.ShouldBeTrue();
        for (var index = 1; index <= 3; index++)
        {
            (await runtime.AddSnapshotAsync(new()
            {
                Identity = ProfilingIdentityFactory.CreateRuntimeSnapshot(), SessionId = session.Identity.Id, SessionKey = session.Identity.Key,
                NodeId = h.Node.Identity.Id, NodeKey = h.Node.Identity.Key, HostName = h.Node.HostName, ProcessId = h.Node.ProcessId,
                TimestampUtc = started.AddSeconds(index), Sequence = index, CaptureStartedElapsed = TimeSpan.FromSeconds(index),
            })).IsSuccess.ShouldBeTrue();
        }

        var record = h.Record() with { StartedUtc = started.AddSeconds(1.5), CompletedUtc = started.AddSeconds(2.5), Duration = TimeSpan.FromSeconds(1),
            WallTime = [new() { Kind = ProfilingWallTimeKind.OutsideSegments, Duration = TimeSpan.FromSeconds(1) }] };
        (await h.Store.AppendAsync([h.Envelope(record, 2)])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);
        record = (await h.Store.FindAsync(record.Id)).Value;
        var sut = new OperationRuntimeCorrelationService((IRuntimeProfilingCorrelationStore)runtime);
        var overlay = (await sut.GetOverlayAsync(record)).Value;
        overlay.InsideSnapshotCount.ShouldBe(1);
        overlay.Samples.Select(value => value.Relation).ShouldBe(new[] { "Before", "Inside", "After" });
        overlay.Samples[1].MetricIntervalFromUtc.ShouldBe(started.AddSeconds(1));
        (await sut.GetOverlayAsync(record with { Node = h.Node with { ProcessStartedUtc = h.Node.ProcessStartedUtc.AddTicks(1) } })).Value.Available.ShouldBeFalse();
        (await sut.GetOverlayAsync(record with { Node = h.Node with { Identity = ProfilingIdentityFactory.CreateNode() } })).Value.Available.ShouldBeFalse();
        (await new OperationRuntimeCorrelationService((IRuntimeProfilingCorrelationStore)runtime, 2).GetOverlayAsync(record)).Errors.ShouldContain(error => error is ProfilingQueryLimitError);
        (await runtime.UpsertParticipationAsync(participation with { State = RuntimeProfilingParticipationState.Completed, CompletedUtc = started.AddSeconds(3) })).IsSuccess.ShouldBeTrue();
        (await sut.GetOverlayAsync(record with { StartedUtc = started.AddSeconds(5), CompletedUtc = started.AddSeconds(6) })).Value.Available.ShouldBeFalse();
    }
}
