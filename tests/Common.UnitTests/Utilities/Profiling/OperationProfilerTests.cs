// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Collections.Concurrent;
using Microsoft.Extensions.Time.Testing;

public sealed class OperationProfilerTests
{
    [Fact]
    public void BeginOperation_EnrichmentAndCompletion_FreezesUtcMetadataWithoutStorage()
    {
        // Arrange
        var (profiler, sink, clock) = Create();
        var scope = profiler.BeginOperation("reports", OperationProfilingKind.Service);
        var id = scope.Id;

        // Act
        profiler.SetKey("reports:refresh");
        profiler.SetDimension("tenant", "sample");
        profiler.SetDimension("number", long.MaxValue);
        profiler.SetDimension("asOf", clock.GetUtcNow());
        profiler.SetMeasurement("items", 42, "count");
        clock.Advance(TimeSpan.FromMilliseconds(250));
        scope.Complete();
        scope.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Id.ShouldBe(id);
        id.ShouldNotBe(Guid.Empty);
        record.Key.ShouldBe("reports:refresh");
        record.Kind.ShouldBe("Service");
        record.Duration.ShouldBe(TimeSpan.FromMilliseconds(250));
        record.StartedUtc.Offset.ShouldBe(TimeSpan.Zero);
        record.CompletedUtc.Offset.ShouldBe(TimeSpan.Zero);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Dimensions.Single(d => d.Key == "number").Value.Scalar.ShouldBe(long.MaxValue.ToString());
        record.Measurements.ShouldHaveSingleItem().Value.Scalar.ShouldBe("42");
        record.NodeId.ShouldNotBe(Guid.Empty);
        profiler.ActiveCount.ShouldBe(0);
        profiler.ActiveBytes.ShouldBe(0);
        profiler.Current.ShouldBeNull();
        ((ICollection<ProfilingDimension>)record.Dimensions).IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void InvalidReplacement_PreservesPreviousValueAndMarksQuality()
    {
        // Arrange
        var (profiler, sink, _) = Create();
        var operation = profiler.BeginOperation("valid");
        operation.SetDimension("value", "accepted");

        // Act
        operation.SetKey(new string('x', 129));
        operation.SetDimension("VALUE", new UnsafeValue());
        operation.SetDimension("VALUE", null);
        operation.SetMeasurement("items", double.NaN, "count");
        operation.Complete();
        operation.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Key.ShouldBe("valid");
        record.Dimensions.ShouldHaveSingleItem().Value.Scalar.ShouldBe("accepted");
        record.Measurements.ShouldBeEmpty();
        record.Quality.RejectedMetadataCount.ShouldBe(4);
        record.Quality.PartialCoverage.ShouldBeTrue();
    }

    [Fact]
    public void RejectedOperation_IsolatesNestedWorkAndRestoresOuterScope()
    {
        // Arrange
        var (profiler, sink, _) = Create(o => o.MaxActiveOperations = 1);
        var outer = profiler.BeginOperation("outer");

        // Act
        var rejected = profiler.BeginOperation("rejected");
        rejected.Id.ShouldNotBe(Guid.Empty);
        rejected.IsRecording.ShouldBeFalse();
        using (var child = profiler.BeginSegment("child"))
        {
            child.SetDimension("leaked", true);
            child.Complete();
        }

        rejected.Dispose();
        profiler.Current.ShouldBeSameAs(outer);
        outer.Complete();
        outer.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Key.ShouldBe("outer");
        record.Segments.ShouldBeEmpty();
        record.Dimensions.ShouldBeEmpty();
        profiler.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public void SuppressionAndExecutionBoundary_RestoreOwnershipWithoutReplacementRoots()
    {
        // Arrange
        var (profiler, sink, _) = Create();
        var outer = profiler.BeginOperation("outer");

        // Act
        using (profiler.Suppress())
        {
            using var excluded = profiler.BeginOperation("excluded");
            excluded.Id.ShouldBe(Guid.Empty);
            excluded.IsRecording.ShouldBeFalse();
        }

        using (profiler.BeginExecutionBoundary())
        {
            profiler.Current.ShouldBeNull();
            using var independent = profiler.BeginOperation("worker");
            independent.Complete();
        }

        profiler.Current.ShouldBeSameAs(outer);
        outer.Complete();
        outer.Dispose();

        // Assert
        sink.Records.Select(r => r.Key).ShouldBe(["worker", "outer"]);
        profiler.Current.ShouldBeNull();
    }

    [Fact]
    public void SourceMetadata_DefensiveCopy_DoesNotRetainMutableCollections()
    {
        // Arrange
        var (profiler, sink, _) = Create();
        var fields = new List<ProfilingDimension> { new() { Key = "dependency", Value = new(ProfilingValueType.String, "sql") } };
        var operation = profiler.BeginOperation("read");

        // Act
        operation.SetSource(new ProfilingAdapterMetadata { Kind = "Database", Fields = fields });
        fields.Clear();
        operation.Complete();
        operation.Dispose();

        // Assert
        sink.Records.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem().Fields.ShouldHaveSingleItem().Value.Scalar.ShouldBe("sql");
    }

    [Fact]
    public void SetupOptions_AreSnapshotted_AndDisabledCaptureIsSilent()
    {
        // Arrange
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        var sink = new CaptureSink();
        var profiler = new OperationProfiler(options, new ProfilingNodeIdentityProvider(), sink);
        options.Operations.MaxKeyLength = 1;
        options.Enabled = false;

        // Act
        using (var operation = profiler.BeginOperation("still-valid"))
        {
            operation.Complete();
        }

        var disabled = new OperationProfiler(options, new ProfilingNodeIdentityProvider(), sink);
        using (var operation = disabled.BeginOperation("disabled"))
        {
            operation.Id.ShouldBe(Guid.Empty);
            operation.Complete();
        }

        // Assert
        sink.Records.ShouldHaveSingleItem().Key.ShouldBe("still-valid");
        disabled.Counters.Attempted.ShouldBe(0);
    }

    [Fact]
    public void PayloadAndEntryLimits_RejectIncomingMetadataAndReleaseAdmission()
    {
        // Arrange
        var (profiler, sink, _) = Create(o => { o.MaxMetadataEntries = 1; o.MaxRecordBytes = 2600; });
        var operation = profiler.BeginOperation("bounded");
        operation.SetDimension("entry", "accepted");

        // Act
        operation.SetDimension("other", "rejected");
        operation.SetDimension("ENTRY", new string('x', 256));
        operation.Complete();
        operation.Dispose();
        using (var next = profiler.BeginOperation("next"))
        {
            next.IsRecording.ShouldBeTrue();
            next.Complete();
        }

        // Assert
        var record = sink.Records.First();
        record.Dimensions.ShouldHaveSingleItem().Value.Scalar.ShouldBe("accepted");
        record.EstimatedPayloadBytes.ShouldBeLessThanOrEqualTo(2600);
        record.Quality.RejectedMetadataCount.ShouldBe(2);
        profiler.ActiveBytes.ShouldBe(0);
    }

    [Fact]
    public void OptionalRuntimeHintFailure_DoesNotRejectAnOperation()
    {
        // Arrange
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        var sink = new CaptureSink();
        var profiler = new OperationProfiler(options, new ProfilingNodeIdentityProvider(), sink,
            runtimeHint: () => throw new InvalidOperationException("Runtime unavailable"));

        // Act
        using (var operation = profiler.BeginOperation("independent"))
        {
            operation.IsRecording.ShouldBeTrue();
            operation.Complete();
        }

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.RuntimeSessionIdAtStart.ShouldBeNull();
        record.RuntimeSessionKeyAtStart.ShouldBeNull();
        profiler.ActiveCount.ShouldBe(0);
    }

    internal static (OperationProfiler Profiler, CaptureSink Sink, FakeTimeProvider Clock) Create(Action<OperationProfilingOptions> configure = null)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        configure?.Invoke(options.Operations);
        var sink = new CaptureSink();
        return (new OperationProfiler(options, new ProfilingNodeIdentityProvider(clock), sink, clock), sink, clock);
    }

    internal sealed class CaptureSink : IOperationProfilingCompletionSink
    {
        public ConcurrentQueue<OperationProfilingRecord> Records { get; } = new();
        public bool TryEnqueue(OperationProfilingRecord record) { this.Records.Enqueue(record); return true; }
    }

    private sealed class UnsafeValue
    {
        public override string ToString() => throw new InvalidOperationException("Application object must never be inspected.");
    }
}
