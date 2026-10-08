// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

public sealed class OperationProfilingLoggingTests
{
    [Fact]
    public void Lifecycle_UsesStableTraceEvents_InitialAndFinalKeys_AndPairedInvocationSequences()
    {
        // Arrange
        var operations = new CapturingLogger<OperationProfiler>();
        var segments = new CapturingLogger<ProfilingSegmentScope>();
        var (profiler, sink, clock) = Create(operations, segments);

        // Act
        using (var root = profiler.BeginOperation(new OperationProfilingStartRequest { Key = "initial", Kind = "Service", CorrelationId = "correlation" }))
        {
            root.SetKey("final");
            root.RunSegment("Load", load =>
            {
                load.RunSegment("Read", _ => clock.Advance(TimeSpan.FromMilliseconds(10)));
                load.RunSegment("Read", _ => clock.Advance(TimeSpan.FromMilliseconds(20)));
            });
            root.Complete();
            root.Dispose();
            root.Dispose();
        }

        // Assert
        var rootEvents = operations.Events.ToArray();
        rootEvents.Length.ShouldBe(2);
        rootEvents[0].Id.ShouldBe(new EventId(6101, "ProfilingOperationStarted"));
        rootEvents[1].Id.ShouldBe(new EventId(6102, "ProfilingOperationStopped"));
        rootEvents[0].Properties["Key"].ShouldBe("initial");
        rootEvents[1].Properties["Key"].ShouldBe("final");
        rootEvents[0].Properties["CorrelationId"].ShouldBe("correlation");
        rootEvents[1].Properties["DurationMs"].ShouldBe(30d);
        var segmentEvents = segments.Events.ToArray();
        segmentEvents.Length.ShouldBe(6);
        segmentEvents.Count(e => e.Id.Name == "ProfilingSegmentStarted").ShouldBe(3);
        segmentEvents.Count(e => e.Id.Name == "ProfilingSegmentStopped").ShouldBe(3);
        segmentEvents.GroupBy(e => (long)e.Properties["InvocationSequence"]).All(g => g.Count() == 2).ShouldBeTrue();
        segmentEvents.Any(e => (string)e.Properties["Path"] == "[\"Load\",\"Read\"]").ShouldBeTrue();
        operations.Events.Concat(segments.Events).All(e => e.Level == LogLevel.Trace).ShouldBeTrue();
        sink.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public void DisabledSuppressedAndRejectedCapture_ProducesNoEvents()
    {
        // Arrange
        var operations = new CapturingLogger<OperationProfiler>();
        var segments = new CapturingLogger<ProfilingSegmentScope>();
        var (profiler, _, _) = Create(operations, segments, o => o.MaxActiveOperations = 1);

        // Act
        using (profiler.Suppress())
        {
            profiler.RunOperation("suppressed", root => root.RunSegment("hidden", _ => { }));
        }

        operations.Events.ShouldBeEmpty();
        using (var root = profiler.BeginOperation("accepted"))
        {
            operations.Events.Count.ShouldBe(1);
            profiler.RunOperation("rejected", r => r.RunSegment("hidden", _ => { }));
            root.RunSegment(new string('x', 129), r => r.RunSegment("hidden", _ => { }));
            root.Complete();
        }

        var options = new ProfilingOptions();
        var disabled = new OperationProfiler(options, new ProfilingNodeIdentityProvider(), new OperationProfilerTests.CaptureSink(), operationLogger: operations, segmentLogger: segments);
        disabled.RunOperation("disabled", root => root.RunSegment("hidden", _ => { }));

        // Assert
        operations.Events.Count.ShouldBe(2);
        segments.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ParentDrivenClosure_EmitsOneIncompleteStop_DespiteLateDisposal()
    {
        // Arrange
        var operations = new CapturingLogger<OperationProfiler>();
        var segments = new CapturingLogger<ProfilingSegmentScope>();
        var (profiler, _, clock) = Create(operations, segments);
        using var root = profiler.BeginOperation("root");
        var parent = root.BeginSegment("Load");
        var child = parent.BeginSegment("Read");

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(10));
        parent.Complete();
        parent.Dispose();
        child.Complete();
        child.Dispose();
        child.Dispose();
        root.Complete();
        root.Dispose();

        // Assert
        var stops = segments.Events.Where(e => e.Id.Name == "ProfilingSegmentStopped").ToArray();
        stops.Length.ShouldBe(2);
        stops.Single(e => (string)e.Properties["Path"] == "[\"Load\",\"Read\"]").Properties["Outcome"].ShouldBe("Incomplete");
        operations.Events.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoggerFaults_PreserveBusinessExceptionAndReturnValue_WithoutRecursiveLogging(bool throwsFromFilter)
    {
        // Arrange
        var operations = new CapturingLogger<OperationProfiler> { ThrowLog = !throwsFromFilter, ThrowFilter = throwsFromFilter };
        var segments = new CapturingLogger<ProfilingSegmentScope> { ThrowLog = !throwsFromFilter, ThrowFilter = throwsFromFilter };
        var (profiler, sink, _) = Create(operations, segments);
        var expected = new InvalidOperationException("secret");
        var calls = 0;

        // Act
        var result = profiler.RunOperation("success", root => root.RunSegment("Read", _ => { calls++; return 42; }));
        var actual = Should.Throw<InvalidOperationException>(() => profiler.RunOperation<int>("failure", root => root.RunSegment<int>("Read", _ => { calls++; throw expected; })));

        // Assert
        result.ShouldBe(42);
        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(2);
        sink.Records.Select(r => r.Outcome).ShouldBe([OperationProfilingOutcome.Completed, OperationProfilingOutcome.Failed]);
        profiler.Counters.LoggingFaults.ShouldBe(8);
        operations.Events.ShouldBeEmpty();
        segments.Events.ShouldBeEmpty();
        profiler.ActiveBytes.ShouldBe(0);
    }

    [Fact]
    public void FilteredLifecycle_DoesNotAllocateLogOnlyState()
    {
        // Arrange
        var operations = new CapturingLogger<OperationProfiler> { Enabled = false };
        var segments = new CapturingLogger<ProfilingSegmentScope> { Enabled = false };
        var logger = new ProfilingLifecycleLogger(operations, segments, new OperationProfilingCaptureCounters());
        var id = Guid.NewGuid();
        var path = new ProfilingSegmentPath(["Load", "Read"]);
        var record = new OperationProfilingRecord { Id = id, Key = "root", Kind = "Service" };
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            Observe();
        }

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10000; iteration++)
        {
            Observe();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        allocated.ShouldBe(0);
        operations.Events.ShouldBeEmpty();
        segments.Events.ShouldBeEmpty();

        void Observe()
        {
            logger.StartOperation(id, "root", "Service", null);
            logger.StopOperation(record);
            logger.StartSegment(id, path, 1);
            logger.StopSegment(id, path, 1, ProfilingSegmentOutcome.Completed, TimeSpan.Zero, null);
        }
    }

    private static (OperationProfiler Profiler, OperationProfilerTests.CaptureSink Sink, FakeTimeProvider Clock) Create(
        CapturingLogger<OperationProfiler> operations, CapturingLogger<ProfilingSegmentScope> segments, Action<OperationProfilingOptions> configure = null)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        configure?.Invoke(options.Operations);
        var sink = new OperationProfilerTests.CaptureSink();
        return (new OperationProfiler(options, new ProfilingNodeIdentityProvider(clock), sink, clock, operationLogger: operations, segmentLogger: segments), sink, clock);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public bool Enabled { get; init; } = true;
        public bool ThrowFilter { get; init; }
        public bool ThrowLog { get; init; }
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => this.ThrowFilter ? throw new InvalidOperationException("filter") : this.Enabled;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (this.ThrowLog)
            {
                throw new InvalidOperationException("sink");
            }

            exception.ShouldBeNull();
            this.Events.Enqueue(new(logLevel, eventId, ((IEnumerable<KeyValuePair<string, object>>)state).ToDictionary(p => p.Key, p => p.Value)));
        }
    }

    private sealed record LogEvent(LogLevel Level, EventId Id, IReadOnlyDictionary<string, object> Properties);
}
