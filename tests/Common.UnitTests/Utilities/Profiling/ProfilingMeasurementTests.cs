// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

public class ProfilingMeasurementTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BeginAsync_WithoutActiveSession_OwnsAndStopsCreatedSession()
    {
        // Arrange
        var harness = await MeasurementHarness.CreateAsync();

        // Act
        var beginResult = await harness.Sut.BeginAsync("load");
        await beginResult.Value.DisposeAsync();
        var data = await harness.Store.GetSessionDataAsync(beginResult.Value.SessionKey);

        // Assert
        beginResult.IsSuccess.ShouldBeTrue();
        harness.Control.StartCount.ShouldBe(1);
        harness.Control.StopCount.ShouldBe(1);
        data.Value.Session.State.ShouldBe(RuntimeProfilingSessionState.Stopped);
        data.Value.Segments.ShouldHaveSingleItem()
            .Outcome.ShouldBe(ProfilingSegmentOutcome.Completed);
    }

    [Fact]
    public async Task BeginAsync_WithActiveSession_JoinsWithoutStoppingSession()
    {
        // Arrange
        var harness = await MeasurementHarness.CreateAsync(createActiveSession: true);

        // Act
        var beginResult = await harness.Sut.BeginAsync("joined");
        await beginResult.Value.DisposeAsync();
        var activeResult = await harness.Store.GetActiveSessionAsync();

        // Assert
        beginResult.IsSuccess.ShouldBeTrue();
        harness.Control.StartCount.ShouldBe(0);
        harness.Control.StopCount.ShouldBe(0);
        activeResult.IsSuccess.ShouldBeTrue();
        activeResult.Value.Identity.Key.ShouldBe(beginResult.Value.SessionKey);
    }

    [Fact]
    public async Task BeginAsync_NestedScopes_AssignsSameNodeParentAndAllowsOverlap()
    {
        // Arrange
        var harness = await MeasurementHarness.CreateAsync(createActiveSession: true);
        var outerResult = await harness.Sut.BeginAsync("outer");

        // Act
        var innerResult = await harness.Sut.BeginAsync("inner");
        await outerResult.Value.DisposeAsync();
        await innerResult.Value.DisposeAsync();
        var data = await harness.Store.GetSessionDataAsync(outerResult.Value.SessionKey);

        // Assert
        data.Value.Segments.Count.ShouldBe(2);
        var outer = data.Value.Segments.Single(segment => segment.Name == "outer");
        var inner = data.Value.Segments.Single(segment => segment.Name == "inner");
        inner.ParentSegmentId.ShouldBe(outer.Id);
        inner.SessionId.ShouldBe(outer.SessionId);
        inner.NodeId.ShouldBe(outer.NodeId);
        outer.Outcome.ShouldBe(ProfilingSegmentOutcome.Completed);
        inner.Outcome.ShouldBe(ProfilingSegmentOutcome.Completed);
    }

    [Fact]
    public async Task DisposeAsync_RawFailure_StoresSafeExceptionMetadataWithoutStackTrace()
    {
        // Arrange
        var harness = await MeasurementHarness.CreateAsync(createActiveSession: true);
        var beginResult = await harness.Sut.BeginAsync("failure");
        var exception = new InvalidOperationException("expected failure");
        beginResult.Value.MarkFailed(exception);

        // Act
        await beginResult.Value.DisposeAsync();
        var data = await harness.Store.GetSessionDataAsync(beginResult.Value.SessionKey);
        var segment = data.Value.Segments.ShouldHaveSingleItem();
        var json = JsonSerializer.Serialize(segment);

        // Assert
        segment.Outcome.ShouldBe(ProfilingSegmentOutcome.Failed);
        segment.ExceptionType.ShouldBe(typeof(InvalidOperationException).FullName);
        segment.ExceptionMessage.ShouldBe("expected failure");
        json.ShouldNotContain("StackTrace");
    }

    [Fact]
    public async Task DisposeAsync_RawCancellation_StoresCancellationOutcome()
    {
        // Arrange
        var harness = await MeasurementHarness.CreateAsync(createActiveSession: true);
        var beginResult = await harness.Sut.BeginAsync("cancelled raw scope");
        beginResult.Value.MarkCancelled();

        // Act
        await beginResult.Value.DisposeAsync();
        var data = await harness.Store.GetSessionDataAsync(beginResult.Value.SessionKey);

        // Assert
        var segment = data.Value.Segments.ShouldHaveSingleItem();
        segment.Outcome.ShouldBe(ProfilingSegmentOutcome.Canceled);
        segment.ExceptionType.ShouldBeNull();
        segment.ExceptionMessage.ShouldBeNull();
    }

    [Fact]
    public async Task MeasureAsync_ThrowingOperation_RecordsFailureAndRethrowsOriginalException()
    {
        // Arrange
        var harness = await MeasurementHarness.CreateAsync(createActiveSession: true);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            harness.Sut.MeasureAsync(
                "throwing",
                _ => throw new InvalidOperationException("application failure")
            )
        );
        var data = await harness.Store.GetSessionDataAsync(harness.ActiveSession.Identity.Key);

        // Assert
        exception.Message.ShouldBe("application failure");
        var segment = data.Value.Segments.ShouldHaveSingleItem();
        segment.Outcome.ShouldBe(ProfilingSegmentOutcome.Failed);
        segment.ExceptionMessage.ShouldBe("application failure");
    }

    [Fact]
    public async Task MeasureAsync_CancelledOperation_RecordsCancellationAndRethrows()
    {
        // Arrange
        var harness = await MeasurementHarness.CreateAsync(createActiveSession: true);

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() =>
            harness.Sut.MeasureAsync(
                "cancelled",
                _ => throw new OperationCanceledException("cancelled")
            )
        );
        var data = await harness.Store.GetSessionDataAsync(harness.ActiveSession.Identity.Key);

        // Assert
        data.Value.Segments.ShouldHaveSingleItem()
            .Outcome.ShouldBe(ProfilingSegmentOutcome.Canceled);
    }

    [Fact]
    public async Task DisposeAsync_AfterCollectionDuration_ClosesSegmentWithoutStoppingSession()
    {
        // Arrange
        var harness = await MeasurementHarness.CreateAsync(duration: TimeSpan.FromSeconds(1));
        var beginResult = await harness.Sut.BeginAsync("long operation");
        harness.Time.Advance(TimeSpan.FromSeconds(2));

        // Act
        await beginResult.Value.DisposeAsync();
        var data = await harness.Store.GetSessionDataAsync(beginResult.Value.SessionKey);

        // Assert
        harness.Control.StopCount.ShouldBe(0);
        var segment = data.Value.Segments.ShouldHaveSingleItem();
        segment.Outcome.ShouldBe(ProfilingSegmentOutcome.Completed);
        segment.CollectionEndedBeforeOperation.ShouldBeTrue();
        segment.Elapsed.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task FinalizeAsync_IncompleteNodeWithOpenSegment_MarksSegmentInterrupted()
    {
        // Arrange
        var harness = await MeasurementHarness.CreateAsync(
            createActiveSession: true,
            duration: TimeSpan.FromSeconds(1)
        );
        var beginResult = await harness.Sut.BeginAsync("abandoned");
        var data = await harness.Store.GetSessionDataAsync(beginResult.Value.SessionKey);
        var segment = data.Value.Segments.ShouldHaveSingleItem();
        var node = harness.Active.Current.Node;
        await harness.Store.UpsertParticipationAsync(
            new()
            {
                SessionId = harness.ActiveSession.Identity.Id,
                SessionKey = harness.ActiveSession.Identity.Key,
                NodeId = node.Identity.Id,
                NodeKey = node.Identity.Key,
                Role = RuntimeProfilingNodeRole.ExpectedParticipant,
                State = RuntimeProfilingParticipationState.Failed,
                JoinedUtc = StartUtc,
                CompletedUtc = StartUtc.AddSeconds(1),
                Failure = "process ended",
            }
        );
        harness.Time.Advance(TimeSpan.FromSeconds(3));
        var finalizer = new RuntimeProfilingSessionFinalizer(harness.Store, harness.Options, harness.Time);

        // Act
        var result = await finalizer.FinalizeAsync(harness.ActiveSession);
        data = await harness.Store.GetSessionDataAsync(beginResult.Value.SessionKey);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.State.ShouldBe(RuntimeProfilingSessionState.CompletedWithWarnings);
        var interrupted = data.Value.Segments.Single(candidate => candidate.Id == segment.Id);
        interrupted.Outcome.ShouldBe(ProfilingSegmentOutcome.Incomplete);
        interrupted.EndedUtc.ShouldBeNull();
        interrupted.CollectionEndedBeforeOperation.ShouldBeTrue();
    }

    private sealed class MeasurementHarness(
        RuntimeProfilingMeasurementService sut,
        InMemoryRuntimeProfilingStore store,
        TestProfilingControlService control,
        FakeTimeProvider time,
        ProfilingOptions options,
        RuntimeProfilingSegmentContext segments,
        RuntimeProfilingActiveSessionContext active,
        RuntimeProfilingSession activeSession
    )
    {
        public RuntimeProfilingMeasurementService Sut { get; } = sut;

        public InMemoryRuntimeProfilingStore Store { get; } = store;

        public TestProfilingControlService Control { get; } = control;

        public FakeTimeProvider Time { get; } = time;

        public ProfilingOptions Options { get; } = options;

        public RuntimeProfilingSegmentContext Segments { get; } = segments;

        public RuntimeProfilingActiveSessionContext Active { get; } = active;

        public RuntimeProfilingSession ActiveSession { get; } = activeSession;

        public static async Task<MeasurementHarness> CreateAsync(
            bool createActiveSession = false,
            TimeSpan? duration = null
        )
        {
            var time = new FakeTimeProvider(StartUtc);
            var options = new ProfilingOptions { Enabled = true, Runtime = new() { Enabled = true, SamplingInterval = TimeSpan.FromSeconds(1), Duration = duration ?? TimeSpan.FromSeconds(30), ParticipationDeadline = TimeSpan.FromSeconds(1), FinalizationGracePeriod = TimeSpan.FromSeconds(1) } };
            var store = new InMemoryRuntimeProfilingStore();
            var registry = new InMemoryBroadcastRegistryStore(new BroadcastingOptions(), time);
            var identity = new TestBroadcastNodeIdentityProvider();
            await registry.UpsertAsync(
                new(
                    identity.GetNodeIdentity(),
                    null,
                    [BroadcastingOptions.DefaultScope],
                    StartUtc.Subtract(TimeSpan.FromMinutes(1)),
                    StartUtc,
                    null
                )
            );
            RuntimeProfilingSession activeSession = null;
            if (createActiveSession)
            {
                activeSession = (
                    await store.GetOrCreateActiveSessionAsync(
                        new(
                            ProfilingIdentityFactory.CreateRuntimeSession(),
                            "active",
                            StartUtc,
                            options.Runtime.SamplingInterval,
                            options.Runtime.Duration,
                            []
                        )
                    )
                )
                    .Value
                    .Session;
            }

            var control = new TestProfilingControlService(store, options, time);
            var segments = new RuntimeProfilingSegmentContext();
            var active = new RuntimeProfilingActiveSessionContext();
            var sut = new RuntimeProfilingMeasurementService(
                options,
                control,
                store,
                new RuntimeProfilingNodeRegistrationAdapter(store, new ProfilingNodeIdentityProvider()),
                registry,
                identity,
                active,
                segments,
                time
            );
            return new(sut, store, control, time, options, segments, active, activeSession);
        }
    }

    private sealed class TestBroadcastNodeIdentityProvider : IBroadcastNodeIdentityProvider
    {
        public string GetNodeIdentity() => "profiling-measurement-node";
    }

    private sealed class TestProfilingControlService(
        IRuntimeProfilingStore store,
        ProfilingOptions options,
        TimeProvider timeProvider
    ) : IRuntimeProfilingControlService
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Task<IResult<RuntimeProfilingStatus>> GetStatusAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public async Task<IResult<RuntimeProfilingControlResult>> StartAsync(
            RuntimeProfilingStartRequest request,
            CancellationToken cancellationToken = default
        )
        {
            this.StartCount++;
            var now = timeProvider.GetUtcNow();
            var result = await store.GetOrCreateActiveSessionAsync(
                new(
                    ProfilingIdentityFactory.CreateRuntimeSession(),
                    request.Name,
                    now,
                    request.SamplingInterval ?? options.Runtime.SamplingInterval,
                    request.Duration ?? options.Runtime.Duration,
                    request.Tags ?? []
                ),
                cancellationToken
            );
            return result.IsSuccess
                ? Result<RuntimeProfilingControlResult>.Success(
                    new(result.Value.Session, result.Value.Created, [])
                )
                : Result<RuntimeProfilingControlResult>
                    .Failure()
                    .WithErrors(result.Errors)
                    .WithMessages(result.Messages);
        }

        public async Task<IResult<RuntimeProfilingControlResult>> StopAsync(
            CancellationToken cancellationToken = default
        )
        {
            this.StopCount++;
            var active = await store.GetActiveSessionAsync(cancellationToken);
            if (active.IsFailure)
            {
                return Result<RuntimeProfilingControlResult>
                    .Failure()
                    .WithErrors(active.Errors)
                    .WithMessages(active.Messages);
            }

            var stopped = await store.TryTransitionSessionAsync(
                active.Value.Identity.Id,
                [RuntimeProfilingSessionState.Running],
                RuntimeProfilingSessionState.Stopped,
                timeProvider.GetUtcNow(),
                cancellationToken
            );
            return stopped.IsSuccess
                ? Result<RuntimeProfilingControlResult>.Success(new(stopped.Value, false, []))
                : Result<RuntimeProfilingControlResult>
                    .Failure()
                    .WithErrors(stopped.Errors)
                    .WithMessages(stopped.Messages);
        }

        public Task<IResult<RuntimeProfilingControlResult>> SnapshotAsync(
            string standaloneSessionName = null,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<RuntimeProfilingControlResult>> CollectGarbageAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<ProfilingMarker>> AddMarkerAsync(
            string name,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<RuntimeProfilingControlResult>> RestartAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<bool>> DeleteSessionAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<int>> DeleteUnpinnedSessionsAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IResult<ProfilingClearResult>> ClearAsync(
            bool confirmed,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
