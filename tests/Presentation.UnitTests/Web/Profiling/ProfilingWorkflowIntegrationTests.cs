// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[Collection(ProfilingWorkflowIntegrationCollection.Name)]
public sealed class ProfilingWorkflowIntegrationTests
{
    [Fact]
    public async Task SingleNodeWorkflow_StartCollectMarkAnalyzeStopExportClear_CompletesEndToEnd()
    {
        // Arrange
        var registry = new SharedBroadcastRegistry();
        await using var node = CreateNode("node-a", registry);
        await node.StartAsync();
        await WaitForTargetsAsync(registry, 1);
        var control = node.Services.GetRequiredService<IRuntimeProfilingControlService>();
        var queries = node.Services.GetRequiredService<IRuntimeProfilingQueryService>();
        var store = node.Services.GetRequiredService<IRuntimeProfilingStore>();

        // Act
        var started = await control.StartAsync(new("single-node", Duration: TimeSpan.FromSeconds(10)));
        var marked = await control.AddMarkerAsync("load");
        var firstCapture = await control.SnapshotAsync();
        var secondCapture = await control.SnapshotAsync();
        await WaitForSessionDataAsync(
            store,
            started.Value.Session.Identity.Key,
            data => data.Snapshots.Count >= 2
        );
        var collected = await queries.GetSessionAsync(started.Value.Session.Identity.Key);
        var nodeKey = collected.Value.Participations.ShouldHaveSingleItem().NodeKey;
        var analysis = await queries.EvaluateAsync(
            new(started.Value.Session.Identity.Key, nodeKey)
        );
        var stopped = await control.StopAsync();
        var stoppedData = await WaitForSessionDataAsync(
            store,
            started.Value.Session.Identity.Key,
            data => data.Participations.Count == 1
                && data.Participations[0].State == RuntimeProfilingParticipationState.Stopped
        );
        var exported = await queries.ExportSnapshotsJsonAsync(
            started.Value.Session.Identity.Key,
            nodeKey
        );
        var cleared = await queries.ClearAsync(true);
        var remaining = await queries.ListSessionsAsync();

        // Assert
        started.IsSuccess.ShouldBeTrue();
        started.Value.Created.ShouldBeTrue();
        started.Value.NodeOutcomes.ShouldHaveSingleItem().Outcome.ShouldBe(
            RuntimeProfilingDeliveryOutcome.Accepted
        );
        marked.IsSuccess.ShouldBeTrue();
        marked.Value.Name.ShouldBe("load");
        firstCapture.IsSuccess.ShouldBeTrue();
        secondCapture.IsSuccess.ShouldBeTrue();
        collected.IsSuccess.ShouldBeTrue();
        collected.Value.Snapshots.Count.ShouldBeGreaterThanOrEqualTo(2);
        analysis.IsSuccess.ShouldBeTrue();
        analysis.Value.Scope.NodeKey.ShouldBe(nodeKey);
        analysis.Value.Scope.SnapshotCount.ShouldBeGreaterThanOrEqualTo(2);
        stopped.IsSuccess.ShouldBeTrue();
        stopped.Value.Session.State.ShouldBe(RuntimeProfilingSessionState.Stopped);
        exported.IsSuccess.ShouldBeTrue();
        using (var document = JsonDocument.Parse(exported.Value))
        {
            document.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
            document.RootElement.GetArrayLength().ShouldBe(stoppedData.Snapshots.Count);
            exported.Value.ShouldNotContain("signals");
            exported.Value.ShouldNotContain("kpis");
        }

        cleared.IsSuccess.ShouldBeTrue();
        cleared.Value.RemovedSessionCount.ShouldBe(1);
        cleared.Value.RemovedSnapshotCount.ShouldBe(stoppedData.Snapshots.Count);
        remaining.IsSuccess.ShouldBeTrue();
        remaining.Value.ShouldBeEmpty();
        await node.StopAsync();
    }

    [Fact]
    public async Task TwoNodeWorkflow_LateNodeSnapshot_JoinsAsAdHocWithoutChangingExpectedSet()
    {
        // Arrange
        var registry = new SharedBroadcastRegistry();
        var store = new SharedCapabilityProfilingStore();
        await using var nodeA = CreateNode("node-a", registry, store);
        await using var nodeB = CreateNode("node-b", registry, store);
        await nodeA.StartAsync();
        await WaitForTargetsAsync(registry, 1);
        var control = nodeA.Services.GetRequiredService<IRuntimeProfilingControlService>();
        var started = await control.StartAsync(new("late-node", Duration: TimeSpan.FromSeconds(10)));
        started.IsSuccess.ShouldBeTrue();

        // Act
        await nodeB.StartAsync();
        await WaitForTargetsAsync(registry, 2);
        var snapshot = await control.SnapshotAsync();
        var data = await WaitForSessionDataAsync(
            store,
            started.Value.Session.Identity.Key,
            value =>
                value.Participations.Count == 2
                && value.Snapshots.Select(item => item.NodeKey).Distinct().Count() == 2
        );

        // Assert
        started.IsSuccess.ShouldBeTrue();
        started.Value.NodeOutcomes.ShouldHaveSingleItem().Outcome.ShouldBe(
            RuntimeProfilingDeliveryOutcome.Accepted
        );
        snapshot.IsSuccess.ShouldBeTrue();
        snapshot.Value.NodeOutcomes.Count.ShouldBe(2);
        snapshot.Value.NodeOutcomes.ShouldAllBe(outcome =>
            outcome.Outcome == RuntimeProfilingDeliveryOutcome.Accepted
        );
        data.Participations.Count.ShouldBe(2);
        data.Participations.Count(participation =>
            participation.Role == RuntimeProfilingNodeRole.ExpectedParticipant
        ).ShouldBe(1);
        data.Participations.Count(participation =>
            participation.Role == RuntimeProfilingNodeRole.AdHocContributor
        ).ShouldBe(1);
        data.Snapshots.Select(item => item.NodeKey).Distinct().Count().ShouldBe(2);

        (await control.StopAsync()).IsSuccess.ShouldBeTrue();
        await nodeB.StopAsync();
        await nodeA.StopAsync();
    }

    [Fact]
    public async Task TwoNodeWorkflow_MissedStop_IsBestEffortAndPreservesOriginalEnd()
    {
        // Arrange
        var registry = new SharedBroadcastRegistry();
        var store = new SharedCapabilityProfilingStore();
        await using var nodeA = CreateNode("node-a", registry, store);
        await using var nodeB = CreateNode("node-b", registry, store);
        await nodeA.StartAsync();
        await nodeB.StartAsync();
        await WaitForTargetsAsync(registry, 2);
        var control = nodeA.Services.GetRequiredService<IRuntimeProfilingControlService>();
        var started = await control.StartAsync(new("missed-stop", Duration: TimeSpan.FromSeconds(10)));
        started.IsSuccess.ShouldBeTrue();
        started.Value.NodeOutcomes.Count.ShouldBe(2);
        var originalEnd = started.Value.Session.EndsUtc;
        var nodeBRegistration = await registry.FindAsync("node-b");
        var now = DateTimeOffset.UtcNow;
        await registry.UpsertAsync(
            new(
                nodeBRegistration.NodeIdentity,
                new Uri("http://127.0.0.1:1/_bdk/api/broadcasting"),
                nodeBRegistration.Scopes,
                nodeBRegistration.ProcessStartedUtc,
                now,
                now.AddMinutes(1)
            )
        );

        // Act
        var stopped = await control.StopAsync();
        var data = await store.GetSessionDataAsync(started.Value.Session.Identity.Key);

        // Assert
        started.IsSuccess.ShouldBeTrue();
        started.Value.NodeOutcomes.Count.ShouldBe(2);
        data.IsSuccess.ShouldBeTrue();
        data.Value.Participations.Count(participation =>
            participation.Role == RuntimeProfilingNodeRole.ExpectedParticipant
        ).ShouldBe(2);
        stopped.IsSuccess.ShouldBeTrue();
        stopped.Value.Session.State.ShouldBe(RuntimeProfilingSessionState.Stopped);
        stopped.Value.Session.EndsUtc.ShouldBe(originalEnd);
        stopped.Value.NodeOutcomes.Any(outcome =>
            outcome.Outcome != RuntimeProfilingDeliveryOutcome.Accepted
            && outcome.Outcome != RuntimeProfilingDeliveryOutcome.AlreadyProcessed
        ).ShouldBeTrue();
        data.Value.Participations.ShouldContain(participation =>
            participation.State != RuntimeProfilingParticipationState.Completed
        );

        await nodeB.StopAsync();
        await nodeA.StopAsync();
    }

    [Fact]
    public async Task ProcessLocalStores_TwoTargets_RejectBeforeMutationOrHttpPublication()
    {
        // Arrange
        var registry = new SharedBroadcastRegistry();
        await using var nodeA = CreateNode("node-a", registry);
        await using var nodeB = CreateNode("node-b", registry);
        await nodeA.StartAsync();
        await nodeB.StartAsync();
        await WaitForTargetsAsync(registry, 2);
        var nodeAStore = nodeA.Services.GetRequiredService<IRuntimeProfilingStore>();
        var nodeBStore = nodeB.Services.GetRequiredService<IRuntimeProfilingStore>();
        var control = nodeA.Services.GetRequiredService<IRuntimeProfilingControlService>();

        // Act
        var result = await control.StartAsync(new("invalid-local-multi-node"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingSharedStoreRequiredError);
        (await nodeAStore.ListSessionsAsync()).Value.ShouldBeEmpty();
        (await nodeBStore.ListSessionsAsync()).Value.ShouldBeEmpty();
        (await registry.FindAsync("node-a")).LastSuccessUtc.ShouldBeNull();
        (await registry.FindAsync("node-b")).LastSuccessUtc.ShouldBeNull();

        await nodeB.StopAsync();
        await nodeA.StopAsync();
    }

    private static WebApplication CreateNode(
        string identity,
        IBroadcastRegistryStore registry,
        IRuntimeProfilingStore store = null
    )
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(registry);
        if (store is not null)
        {
            builder.Services.AddSingleton(store);
        }

        builder.Services.AddSingleton<IRuntimeProfilingSnapshotProbe, DeterministicSnapshotProbe>();
        builder
            .Services.AddBroadcasting(options =>
                options.Scopes("profiling-test").NodeIdentity(identity)
            )
            .WithHttpTransport(options => options.SharedSecret("profiling-test-secret"));
        builder.Services.AddProfiling(options => options.Enabled()).WithRuntimeProfiling(options => options

                .SamplingInterval(RuntimeProfilingOptions.MinimumSamplingInterval)
                .Duration(TimeSpan.FromSeconds(10))
                .ParticipationDeadline(TimeSpan.FromSeconds(2))
                .FinalizationGracePeriod(TimeSpan.Zero));

        var app = builder.Build();
        app.MapEndpoints();
        return app;
    }

    private static async Task WaitForTargetsAsync(
        IBroadcastRegistryStore registry,
        int expectedCount
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var targets = await registry.GetActiveAsync(["profiling-test"]);
            if (targets.Count == expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        throw new TimeoutException(
            $"Expected {expectedCount} active Profiling Broadcast targets."
        );
    }

    private static async Task<RuntimeProfilingSessionData> WaitForSessionDataAsync(
        IRuntimeProfilingStore store,
        string sessionKey,
        Func<RuntimeProfilingSessionData, bool> condition
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await store.GetSessionDataAsync(sessionKey);
            if (result.IsSuccess && condition(result.Value))
            {
                return result.Value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        throw new TimeoutException(
            $"Profiling session '{sessionKey}' did not reach the expected state."
        );
    }

    private sealed class DeterministicSnapshotProbe : IRuntimeProfilingSnapshotProbe
    {
        public Task<IResult<RuntimeProfilingSnapshot>> CaptureAsync(
            RuntimeProfilingCaptureRequest request,
            CancellationToken cancellationToken = default
        )
        {
            var megabytes = request.Sequence * 1024L * 1024L;
            return Task.FromResult<IResult<RuntimeProfilingSnapshot>>(
                Result<RuntimeProfilingSnapshot>.Success(
                    new()
                    {
                        Identity = ProfilingIdentityFactory.CreateRuntimeSnapshot(),
                        SessionId = request.Session.Identity.Id,
                        SessionKey = request.Session.Identity.Key,
                        NodeId = request.Node.Identity.Id,
                        NodeKey = request.Node.Identity.Key,
                        TimestampUtc = request.Session.StartedUtc.Add(
                            request.CaptureStartedElapsed
                        ),
                        HostName = request.Node.HostName,
                        ProcessId = request.Node.ProcessId,
                        Sequence = request.Sequence,
                        ScheduledElapsed = request.ScheduledElapsed,
                        CaptureStartedElapsed = request.CaptureStartedElapsed,
                        CaptureDuration = TimeSpan.FromMilliseconds(1),
                        SkippedCaptureCount = request.SkippedCaptureCount,
                        FailedCaptureCount = request.FailedCaptureCount,
                        CpuUsagePercent = 40 + request.Sequence,
                        ProcessCpuDuration = TimeSpan.FromMilliseconds(
                            request.Sequence * 100
                        ),
                        LogicalProcessorCount = 1,
                        WorkingSetBytes = 128L * 1024 * 1024 + megabytes,
                        PrivateMemoryBytes = 96L * 1024 * 1024 + megabytes,
                        ManagedMemoryBytes = 32L * 1024 * 1024 + megabytes,
                        ManagedHeapSizeBytes = 32L * 1024 * 1024 + megabytes,
                        TotalAllocatedBytes = request.Sequence * 8L * 1024 * 1024,
                        AllocationRateBytesPerSecond = 8L * 1024 * 1024,
                        Gen0CollectionCount = request.Sequence,
                        Gen1CollectionCount = request.Sequence / 2,
                        Gen2CollectionCount = request.Sequence / 4,
                        CumulativeGcPauseDuration = TimeSpan.FromMilliseconds(
                            request.Sequence * 2
                        ),
                        GcPausePercent = 1,
                        LargeObjectHeapBytes = 4L * 1024 * 1024,
                        HeapFragmentationPercent = 2,
                        LargeObjectHeapFragmentationPercent = 1,
                    }
                )
            );
        }
    }

    private sealed class SharedBroadcastRegistry : IBroadcastRegistryStore
    {
        private readonly InMemoryBroadcastRegistryStore inner = new(
            new BroadcastingOptions(),
            TimeProvider.System
        );

        public BroadcastRegistryCapabilities Capabilities { get; } = new(true, true);

        public Task UpsertAsync(
            BroadcastNodeRegistrationRequest request,
            CancellationToken cancellationToken = default
        ) => this.inner.UpsertAsync(request, cancellationToken);

        public Task RemoveAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => this.inner.RemoveAsync(nodeIdentity, cancellationToken);

        public Task<IReadOnlyList<BroadcastNodeRegistration>> GetActiveAsync(
            IReadOnlyCollection<string> scopes,
            CancellationToken cancellationToken = default
        ) => this.inner.GetActiveAsync(scopes, cancellationToken);

        public Task<BroadcastNodeRegistration> FindAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => this.inner.FindAsync(nodeIdentity, cancellationToken);

        public Task RecordDeliveryAsync(
            string nodeIdentity,
            bool succeeded,
            string failure,
            CancellationToken cancellationToken = default
        ) => this.inner.RecordDeliveryAsync(nodeIdentity, succeeded, failure, cancellationToken);

        public Task RenewLeaseAsync(
            string nodeIdentity,
            DateTimeOffset leaseExpiresUtc,
            CancellationToken cancellationToken = default
        ) => this.inner.RenewLeaseAsync(nodeIdentity, leaseExpiresUtc, cancellationToken);

        public Task ExpireLeasesAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default
        ) => this.inner.ExpireLeasesAsync(utcNow, cancellationToken);

        public Task<IReadOnlyList<BroadcastNodeRegistration>> ListAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.ListAsync(cancellationToken);
    }

    private sealed class SharedCapabilityProfilingStore : IRuntimeProfilingStore
    {
        private readonly InMemoryRuntimeProfilingStore inner = new();

        public Task<IResult<ProfilingNode>> FindNodeAsync(RuntimeProfilingNodeCorrelation correlation, CancellationToken cancellationToken = default)
            => this.inner.FindNodeAsync(correlation, cancellationToken);

        public Task<IResult<RuntimeProfilingSession>> ImportSessionAsync(
            RuntimeProfilingSessionData data,
            CancellationToken cancellationToken = default
        ) => this.inner.ImportSessionAsync(data, cancellationToken);

        public ProfilingStoreCapabilities Capabilities { get; } = new(true);

        public Task<IResult<RuntimeProfilingSessionResolution>> GetOrCreateActiveSessionAsync(
            RuntimeProfilingSessionCreateRequest request,
            CancellationToken cancellationToken = default
        ) => this.inner.GetOrCreateActiveSessionAsync(request, cancellationToken);

        public Task<IResult<RuntimeProfilingSession>> GetActiveSessionAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.GetActiveSessionAsync(cancellationToken);

        public Task<IResult<RuntimeProfilingSession>> FindSessionAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => this.inner.FindSessionAsync(sessionKey, cancellationToken);

        public Task<IResult<IReadOnlyList<RuntimeProfilingSession>>> ListSessionsAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.ListSessionsAsync(cancellationToken);

        public Task<IResult<RuntimeProfilingSession>> UpdateSessionMetadataAsync(
            string sessionKey,
            RuntimeProfilingSessionMetadata metadata,
            CancellationToken cancellationToken = default
        ) => this.inner.UpdateSessionMetadataAsync(sessionKey, metadata, cancellationToken);

        public Task<IResult<RuntimeProfilingSession>> TryTransitionSessionAsync(
            Guid sessionId,
            IReadOnlyCollection<RuntimeProfilingSessionState> expectedStates,
            RuntimeProfilingSessionState nextState,
            DateTimeOffset transitionedUtc,
            CancellationToken cancellationToken = default
        ) =>
            this.inner.TryTransitionSessionAsync(
                sessionId,
                expectedStates,
                nextState,
                transitionedUtc,
                cancellationToken
            );

        public Task<IResult<ProfilingNode>> GetOrCreateNodeAsync(
            RuntimeProfilingNodeCorrelation correlation,
            ProfilingNode proposedNode,
            CancellationToken cancellationToken = default
        ) => this.inner.GetOrCreateNodeAsync(correlation, proposedNode, cancellationToken);

        public Task<IResult<RuntimeProfilingNodeParticipation>> UpsertParticipationAsync(
            RuntimeProfilingNodeParticipation participation,
            CancellationToken cancellationToken = default
        ) => this.inner.UpsertParticipationAsync(participation, cancellationToken);

        public Task<IResult<RuntimeProfilingContext>> AddRuntimeContextAsync(
            RuntimeProfilingContext context,
            CancellationToken cancellationToken = default
        ) => this.inner.AddRuntimeContextAsync(context, cancellationToken);

        public Task<IResult<RuntimeProfilingSnapshot>> AddSnapshotAsync(
            RuntimeProfilingSnapshot snapshot,
            CancellationToken cancellationToken = default
        ) => this.inner.AddSnapshotAsync(snapshot, cancellationToken);

        public Task<IResult<ProfilingMarker>> AddMarkerAsync(
            ProfilingMarker marker,
            CancellationToken cancellationToken = default
        ) => this.inner.AddMarkerAsync(marker, cancellationToken);

        public Task<IResult<ProfilingSegment>> UpsertSegmentAsync(
            ProfilingSegment segment,
            CancellationToken cancellationToken = default
        ) => this.inner.UpsertSegmentAsync(segment, cancellationToken);

        public Task<IResult<ProfilingMetricObservation>> AddMetricObservationAsync(
            ProfilingMetricObservation observation,
            CancellationToken cancellationToken = default
        ) => this.inner.AddMetricObservationAsync(observation, cancellationToken);

        public Task<IResult<RuntimeProfilingSessionData>> GetSessionDataAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => this.inner.GetSessionDataAsync(sessionKey, cancellationToken);

        public Task<IResult<bool>> DeleteSessionAsync(
            string sessionKey,
            CancellationToken cancellationToken = default
        ) => this.inner.DeleteSessionAsync(sessionKey, cancellationToken);

        public Task<IResult<int>> DeleteUnpinnedSessionsAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.DeleteUnpinnedSessionsAsync(cancellationToken);

        public Task<IResult<ProfilingClearResult>> ClearAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.ClearAsync(cancellationToken);

        public Task<IResult<int>> ApplyRetentionAsync(
            int maximumRetainedSessions,
            TimeSpan maximumSessionAge,
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default
        ) =>
            this.inner.ApplyRetentionAsync(
                maximumRetainedSessions,
                maximumSessionAge,
                utcNow,
                cancellationToken
            );
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProfilingWorkflowIntegrationCollection
{
    public const string Name = nameof(ProfilingWorkflowIntegrationCollection);
}
