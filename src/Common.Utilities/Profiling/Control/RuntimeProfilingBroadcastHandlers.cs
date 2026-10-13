// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Provides bounded process-local idempotency for profiling handler side effects.</summary>
/// <example><code>if (tracker.TryBegin(context.BroadcastId)) { /* apply once */ }</code></example>
public sealed class RuntimeProfilingBroadcastExecutionTracker
{
    private const int Capacity = 1024;
    private readonly object sync = new();
    private readonly HashSet<Guid> entries = [];
    private readonly Queue<Guid> insertionOrder = [];

    /// <summary>Reserves a broadcast identifier exactly once within the bounded recent set.</summary>
    /// <param name="broadcastId">The Broadcast publication identifier.</param>
    /// <returns><c>true</c> only for the first observed execution.</returns>
    /// <example><code>var first = tracker.TryBegin(context.BroadcastId);</code></example>
    public bool TryBegin(Guid broadcastId)
    {
        if (broadcastId == Guid.Empty)
        {
            return false;
        }

        lock (this.sync)
        {
            if (!this.entries.Add(broadcastId))
            {
                return false;
            }

            this.insertionOrder.Enqueue(broadcastId);
            while (this.insertionOrder.Count > Capacity)
            {
                this.entries.Remove(this.insertionOrder.Dequeue());
            }

            return true;
        }
    }
}

/// <summary>Admits a deployment-wide session into the node-local collector.</summary>
/// <param name="collector">The process-local profiling collector.</param>
/// <param name="executions">The bounded handler idempotency tracker.</param>
/// <example><code>services.AddBroadcasting().AddHandler&lt;RuntimeProfilingStartBroadcast, RuntimeProfilingStartBroadcastHandler&gt;();</code></example>
public sealed class RuntimeProfilingStartBroadcastHandler(
    IRuntimeProfilingCollector collector,
    RuntimeProfilingBroadcastExecutionTracker executions
) : IBroadcastHandler<RuntimeProfilingStartBroadcast>
{
    /// <inheritdoc />
    public async Task HandleAsync(
        RuntimeProfilingStartBroadcast payload,
        BroadcastContext context,
        CancellationToken cancellationToken
    )
    {
        if (!executions.TryBegin(context.BroadcastId))
        {
            return;
        }

        var result = await collector
            .StartAsync(payload.Session.ToSession(), cancellationToken)
            .ConfigureAwait(false);
        EnsureApplied(result, "start");
    }

    private static void EnsureApplied(IResult result, string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"The profiling {operation} broadcast could not be applied locally."
            );
        }
    }
}

/// <summary>Stops the identified node-local collector on a best-effort basis.</summary>
/// <param name="collector">The process-local profiling collector.</param>
/// <param name="executions">The bounded handler idempotency tracker.</param>
/// <example><code>services.AddBroadcasting().AddHandler&lt;RuntimeProfilingStopBroadcast, RuntimeProfilingStopBroadcastHandler&gt;();</code></example>
public sealed class RuntimeProfilingStopBroadcastHandler(
    IRuntimeProfilingCollector collector,
    RuntimeProfilingBroadcastExecutionTracker executions
) : IBroadcastHandler<RuntimeProfilingStopBroadcast>
{
    /// <inheritdoc />
    public async Task HandleAsync(
        RuntimeProfilingStopBroadcast payload,
        BroadcastContext context,
        CancellationToken cancellationToken
    )
    {
        if (!executions.TryBegin(context.BroadcastId))
        {
            return;
        }

        var result = await collector
            .StopAsync(payload.SessionId, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                "The profiling stop broadcast could not be applied locally."
            );
        }
    }
}

/// <summary>Captures one immediate node-local snapshot for the supplied session.</summary>
/// <param name="collector">The process-local profiling collector.</param>
/// <param name="executions">The bounded handler idempotency tracker.</param>
/// <example><code>services.AddBroadcasting().AddHandler&lt;RuntimeProfilingSnapshotBroadcast, RuntimeProfilingSnapshotBroadcastHandler&gt;();</code></example>
public sealed class RuntimeProfilingSnapshotBroadcastHandler(
    IRuntimeProfilingCollector collector,
    RuntimeProfilingBroadcastExecutionTracker executions
) : IBroadcastHandler<RuntimeProfilingSnapshotBroadcast>
{
    /// <inheritdoc />
    public async Task HandleAsync(
        RuntimeProfilingSnapshotBroadcast payload,
        BroadcastContext context,
        CancellationToken cancellationToken
    )
    {
        if (!executions.TryBegin(context.BroadcastId))
        {
            return;
        }

        var result = await collector
            .CaptureAsync(payload.Session.ToSession(), payload.Role, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                "The profiling snapshot broadcast could not be applied locally."
            );
        }
    }
}

/// <summary>
/// Performs one normal garbage collection and records a local action marker when a session exists.
/// </summary>
/// <param name="store">The profiling session store.</param>
/// <param name="nodes">The stable profiling node provider.</param>
/// <param name="registry">The existing Broadcast registry.</param>
/// <param name="broadcastIdentity">The local Broadcast identity provider.</param>
/// <param name="executions">The bounded handler idempotency tracker.</param>
/// <param name="timeProvider">The UTC clock used for the local marker.</param>
/// <example><code>services.AddBroadcasting().AddHandler&lt;RuntimeProfilingGarbageCollectionBroadcast, RuntimeProfilingGarbageCollectionBroadcastHandler&gt;();</code></example>
public sealed class RuntimeProfilingGarbageCollectionBroadcastHandler(
    IRuntimeProfilingStore store,
    IRuntimeProfilingNodeRegistrationAdapter nodes,
    IBroadcastRegistryStore registry,
    IBroadcastNodeIdentityProvider broadcastIdentity,
    RuntimeProfilingBroadcastExecutionTracker executions,
    TimeProvider timeProvider
) : IBroadcastHandler<RuntimeProfilingGarbageCollectionBroadcast>
{
    /// <inheritdoc />
    public async Task HandleAsync(
        RuntimeProfilingGarbageCollectionBroadcast payload,
        BroadcastContext context,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!executions.TryBegin(context.BroadcastId))
        {
            return;
        }

        GC.Collect();
        if (payload.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(payload.SessionKey))
        {
            return;
        }

        var registration = await registry
            .FindAsync(broadcastIdentity.GetNodeIdentity(), cancellationToken)
            .ConfigureAwait(false);
        if (registration is null || !registration.IsActive)
        {
            throw new InvalidOperationException(
                "The local Broadcast registration is unavailable for the profiling GC marker."
            );
        }

        var nodeResult = await nodes
            .RegisterLocalAsync(registration, cancellationToken)
            .ConfigureAwait(false);
        if (nodeResult.IsFailure)
        {
            throw new InvalidOperationException(
                "The local profiling node is unavailable for the GC marker."
            );
        }

        var markerResult = await store
            .AddMarkerAsync(
                new(
                    Guid.NewGuid(),
                    payload.SessionId,
                    nodeResult.Value.Identity.Id,
                    payload.SessionKey,
                    nodeResult.Value.Identity.Key,
                    "Manual GC",
                    timeProvider.GetUtcNow(),
                    "GarbageCollection"
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (markerResult.IsFailure)
        {
            throw new InvalidOperationException(
                "The profiling GC action marker could not be stored locally."
            );
        }
    }
}
