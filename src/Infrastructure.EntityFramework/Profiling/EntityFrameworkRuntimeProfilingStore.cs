// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Data;
using System.Data.Common;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Stores shared profiling sessions through an application-owned Entity Framework context.
/// </summary>
/// <typeparam name="TContext">
/// The application context implementing <see cref="IProfilingDbContext"/>.
/// </typeparam>
/// <remarks>
/// The singleton provider never retains a scoped context. Every operation owns a fresh dependency
/// injection scope and context, while relational lifecycle mutations use serializable transactions.
/// </remarks>
/// <example>
/// <code>
/// services.AddProfiling(options => options.Enabled()).WithRuntimeProfiling()
///     .WithEntityFrameworkProvider&lt;AppDbContext&gt;();
/// </code>
/// </example>
internal sealed class EntityFrameworkRuntimeProfilingStore<TContext>(IServiceScopeFactory scopeFactory)
    : IRuntimeProfilingStore
    where TContext : DbContext, IProfilingDbContext
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    internal Func<CancellationToken, Task<IResult<ProfilingClearResult>>> SharedClear { get; set; }

    /// <inheritdoc />
    public ProfilingStoreCapabilities Capabilities { get; } = new(true);

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingSessionResolution>> GetOrCreateActiveSessionAsync(
        RuntimeProfilingSessionCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var validation = ValidateSessionRequest(request);
        if (validation is not null)
        {
            return Failure<RuntimeProfilingSessionResolution>(validation);
        }

        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        var maintenance = await EntityFrameworkProfilingRuntimeGate.AcquireAsync(context, token).ConfigureAwait(false);
                        if (maintenance.MaintenanceClearId is not null)
                        {
                            return Failure<RuntimeProfilingSessionResolution>(new ProfilingBusyError("Runtime history is being cleared."));
                        }

                        var active = await context
                            .Set<RuntimeProfilingSessionEntity>().SingleOrDefaultAsync(
                                x =>
                                    x.LifecycleKey
                                    == EntityFrameworkProfilingStoreConstants.ActiveLifecycleKey,
                                token
                            )
                            .ConfigureAwait(false);
                        if (active is not null)
                        {
                            return Success(
                                new RuntimeProfilingSessionResolution(
                                    ProfilingEntityMapper.ToModel(active),
                                    false
                                )
                            );
                        }

                        if (
                            await context
                                .Set<RuntimeProfilingInvalidSessionEntity>().AnyAsync(
                                    x =>
                                        x.Id == request.Identity.Id
                                        || x.Key == request.Identity.Key,
                                    token
                                )
                                .ConfigureAwait(false)
                        )
                        {
                            return Failure<RuntimeProfilingSessionResolution>(
                                new ProfilingInvalidStateError(
                                    "A cleared, deleted, or expired session identity cannot be reused."
                                )
                            );
                        }

                        if (
                            await context
                                .Set<RuntimeProfilingSessionEntity>().AnyAsync(
                                    x =>
                                        x.Id == request.Identity.Id
                                        || x.Key == request.Identity.Key,
                                    token
                                )
                                .ConfigureAwait(false)
                        )
                        {
                            return Failure<RuntimeProfilingSessionResolution>(
                                new ProfilingValidationError(
                                    "The profiling session identity is already in use."
                                )
                            );
                        }

                        var entity = ProfilingEntityMapper.ToEntity(request);
                        context.Set<RuntimeProfilingSessionEntity>().Add(entity);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        return Success(
                            new RuntimeProfilingSessionResolution(
                                ProfilingEntityMapper.ToModel(entity),
                                true
                            )
                        );
                    },
                    cancellationToken,
                    transactional: true
                )
                .ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingSession>> GetActiveSessionAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var entity = await context
            .Set<RuntimeProfilingSessionEntity>().AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.LifecycleKey == EntityFrameworkProfilingStoreConstants.ActiveLifecycleKey,
                cancellationToken
            )
            .ConfigureAwait(false);
        return entity is null
            ? Failure<RuntimeProfilingSession>(
                new ProfilingInvalidStateError("No profiling session is active.")
            )
            : Success(ProfilingEntityMapper.ToModel(entity));
    }

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingSession>> FindSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsPublicKey(sessionKey))
        {
            return Failure<RuntimeProfilingSession>(new ProfilingInvalidKeyError("session"));
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var entity = await context
            .Set<RuntimeProfilingSessionEntity>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == sessionKey, cancellationToken)
            .ConfigureAwait(false);
        return entity is null
            ? Failure<RuntimeProfilingSession>(
                new NotFoundError($"Profiling session '{sessionKey}' was not found.")
            )
            : Success(ProfilingEntityMapper.ToModel(entity));
    }

    /// <inheritdoc />
    public async Task<IResult<IReadOnlyList<RuntimeProfilingSession>>> ListSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var entities = await context
            .Set<RuntimeProfilingSessionEntity>().AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Success<IReadOnlyList<RuntimeProfilingSession>>(
            entities
                .OrderByDescending(x => x.StartedUtc)
                .Select(ProfilingEntityMapper.ToModel)
                .ToArray()
        );
    }

    /// <inheritdoc />
    public Task<IResult<RuntimeProfilingSession>> UpdateSessionMetadataAsync(
        string sessionKey,
        RuntimeProfilingSessionMetadata metadata,
        CancellationToken cancellationToken = default
    )
    {
        if (metadata is null)
        {
            return Task.FromResult<IResult<RuntimeProfilingSession>>(
                Failure<RuntimeProfilingSession>(
                    new ProfilingValidationError("Session metadata is required.")
                )
            );
        }

        return this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var entity = await context
                    .Set<RuntimeProfilingSessionEntity>().SingleOrDefaultAsync(x => x.Key == sessionKey, token)
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return Failure<RuntimeProfilingSession>(
                        new NotFoundError($"Profiling session '{sessionKey}' was not found.")
                    );
                }

                ReplaceItems(
                    entity.Tags,
                    ProfilingEntityMapper.ToSessionTags(entity.Id, metadata.Tags)
                );
                entity.Name = NormalizeOptional(metadata.Name);
                entity.Note = NormalizeOptional(metadata.Note);
                entity.IsPinned = metadata.IsPinned;
                entity.AdvanceConcurrencyVersion();
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return Success(ProfilingEntityMapper.ToModel(entity));
            },
            cancellationToken,
            transactional: true
        );
    }

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingSession>> TryTransitionSessionAsync(
        Guid sessionId,
        IReadOnlyCollection<RuntimeProfilingSessionState> expectedStates,
        RuntimeProfilingSessionState nextState,
        DateTimeOffset transitionedUtc,
        CancellationToken cancellationToken = default
    )
    {
        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        var entity = await context
                            .Set<RuntimeProfilingSessionEntity>().SingleOrDefaultAsync(x => x.Id == sessionId, token)
                            .ConfigureAwait(false);
                        if (entity is null)
                        {
                            return Failure<RuntimeProfilingSession>(
                                new NotFoundError("The profiling session was not found.")
                            );
                        }

                        if (expectedStates?.Contains(entity.State) != true)
                        {
                            return Failure<RuntimeProfilingSession>(
                                new ProfilingInvalidStateError(
                                    $"The session cannot transition from '{entity.State}'."
                                )
                            );
                        }

                        if (!IsValidTransition(entity.State, nextState))
                        {
                            return Failure<RuntimeProfilingSession>(
                                new ProfilingInvalidStateError(
                                    $"The session cannot transition from '{entity.State}' to '{nextState}'."
                                )
                            );
                        }

                        if (entity.State == nextState)
                        {
                            return Success(ProfilingEntityMapper.ToModel(entity));
                        }

                        entity.State = nextState;
                        if (IsTerminal(nextState))
                        {
                            entity.CompletedUtc = transitionedUtc;
                            entity.LifecycleKey = entity.Id.ToString("N");
                        }

                        entity.AdvanceConcurrencyVersion();
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        return Success(ProfilingEntityMapper.ToModel(entity));
                    },
                    cancellationToken,
                    transactional: true
                )
                .ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IResult<ProfilingNode>> FindNodeAsync(RuntimeProfilingNodeCorrelation correlation, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var identity = correlation?.BroadcastNodeIdentity?.Trim();
        var processStart = correlation?.ProcessStartedUtc.ToUniversalTime();
        var entity = await context.Set<ProfilingNodeEntity>().AsNoTracking().SingleOrDefaultAsync(
            node => node.BroadcastNodeIdentity == identity && node.ProcessStartedUtc == processStart,
            cancellationToken).ConfigureAwait(false);
        return Success(entity is null ? null : ProfilingEntityMapper.ToModel(entity));
    }

    /// <inheritdoc />
    public async Task<IResult<ProfilingNode>> GetOrCreateNodeAsync(
        RuntimeProfilingNodeCorrelation correlation,
        ProfilingNode proposedNode,
        CancellationToken cancellationToken = default
    )
    {
        var validation = ValidateNode(correlation, proposedNode);
        if (validation is not null)
        {
            return Failure<ProfilingNode>(validation);
        }

        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        var identity = correlation.BroadcastNodeIdentity.Trim();
                        var entity = await context
                            .Set<ProfilingNodeEntity>().SingleOrDefaultAsync(
                                x =>
                                    x.BroadcastNodeIdentity == identity
                                    && x.ProcessStartedUtc == correlation.ProcessStartedUtc,
                                token
                            )
                            .ConfigureAwait(false);
                        if (entity is not null)
                        {
                            return Success(ProfilingEntityMapper.ToModel(entity));
                        }

                        var shared = await context.Set<ProfilingNodeEntity>().SingleOrDefaultAsync(node => node.Id == proposedNode.Identity.Id || node.Key == proposedNode.Identity.Key, token).ConfigureAwait(false);
                        if (shared is not null)
                        {
                            if (shared.Id != proposedNode.Identity.Id || shared.Key != proposedNode.Identity.Key || shared.BroadcastNodeIdentity is not null
                                || shared.ProcessId != proposedNode.ProcessId || shared.ExecutionStartedUtcTicks != proposedNode.ProcessStartedUtc.UtcTicks
                                || shared.HostName != proposedNode.HostName || shared.DisplayName != proposedNode.DisplayName || shared.ApplicationVersion != proposedNode.ApplicationVersion)
                            {
                                return Failure<ProfilingNode>(new ProfilingValidationError("The profiling node identity is already in use."));
                            }

                            shared.BroadcastNodeIdentity = identity;
                            shared.ProcessStartedUtc = correlation.ProcessStartedUtc;
                            await context.SaveChangesAsync(token).ConfigureAwait(false);
                            return Success(ProfilingEntityMapper.ToModel(shared));
                        }

                        entity = ProfilingEntityMapper.ToEntity(correlation, proposedNode);
                        context.Set<ProfilingNodeEntity>().Add(entity);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        return Success(ProfilingEntityMapper.ToModel(entity));
                    },
                    cancellationToken,
                    transactional: true
                )
                .ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public Task<IResult<RuntimeProfilingNodeParticipation>> UpsertParticipationAsync(
        RuntimeProfilingNodeParticipation participation,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var resolved = await ResolveSessionNodeReferenceAsync(
                        context,
                        participation?.SessionId ?? Guid.Empty,
                        participation?.SessionKey,
                        participation?.NodeId ?? Guid.Empty,
                        participation?.NodeKey,
                        token
                    )
                    .ConfigureAwait(false);
                if (resolved.Error is not null)
                {
                    return Failure<RuntimeProfilingNodeParticipation>(resolved.Error);
                }

                if (
                    participation.SuccessfulCaptureCount < 0
                    || participation.SkippedCaptureCount < 0
                    || participation.FailedCaptureCount < 0
                )
                {
                    return Failure<RuntimeProfilingNodeParticipation>(
                        new ProfilingValidationError(
                            "Participation capture totals cannot be negative."
                        )
                    );
                }

                var entity = await context
                    .Set<RuntimeProfilingParticipationEntity>().SingleOrDefaultAsync(
                        x =>
                            x.SessionId == participation.SessionId
                            && x.NodeId == participation.NodeId,
                        token
                    )
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    entity = ProfilingEntityMapper.ToEntity(participation);
                    context.Set<RuntimeProfilingParticipationEntity>().Add(entity);
                }
                else
                {
                    if (
                        entity.Role != participation.Role
                        || participation.SuccessfulCaptureCount < entity.SuccessfulCaptureCount
                        || participation.SkippedCaptureCount < entity.SkippedCaptureCount
                        || participation.FailedCaptureCount < entity.FailedCaptureCount
                        || IsTerminal(entity.State) && entity.State != participation.State
                        || ParticipationRank(participation.State) < ParticipationRank(entity.State)
                    )
                    {
                        return Failure<RuntimeProfilingNodeParticipation>(
                            new ProfilingInvalidStateError(
                                "Node participation role, state, and capture totals cannot move backwards."
                            )
                        );
                    }

                    entity.State = participation.State;
                    entity.JoinedUtc = participation.JoinedUtc;
                    entity.CompletedUtc = participation.CompletedUtc;
                    entity.SuccessfulCaptureCount = participation.SuccessfulCaptureCount;
                    entity.SkippedCaptureCount = participation.SkippedCaptureCount;
                    entity.FailedCaptureCount = participation.FailedCaptureCount;
                    entity.Failure = NormalizeOptional(participation.Failure);
                    entity.AdvanceConcurrencyVersion();
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return Success(
                    ProfilingEntityMapper.ToModel(entity, resolved.Session.Key, resolved.Node.Key)
                );
            },
            cancellationToken,
            transactional: true
        );

    /// <inheritdoc />
    public Task<IResult<RuntimeProfilingContext>> AddRuntimeContextAsync(
        RuntimeProfilingContext runtimeContext,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var resolved = await ResolveSessionNodeAsync(
                        context,
                        runtimeContext?.SessionId ?? Guid.Empty,
                        runtimeContext?.SessionKey,
                        runtimeContext?.NodeId ?? Guid.Empty,
                        runtimeContext?.NodeKey,
                        token
                    )
                    .ConfigureAwait(false);
                if (resolved.Error is not null)
                {
                    return Failure<RuntimeProfilingContext>(resolved.Error);
                }

                var existing = resolved.Session.RuntimeContexts.SingleOrDefault(x =>
                    x.NodeId == runtimeContext.NodeId
                );
                if (existing is not null)
                {
                    var model = ProfilingEntityMapper.ToModel(
                        existing,
                        resolved.Session.Key,
                        resolved.Node.Key
                    );
                    return model == runtimeContext
                        ? Success(model)
                        : Failure<RuntimeProfilingContext>(
                            new ProfilingInvalidStateError(
                                "Runtime context is immutable once stored for a session node."
                            )
                        );
                }

                resolved.Session.RuntimeContexts.Add(
                    ProfilingEntityMapper.ToEntity(runtimeContext)
                );
                resolved.Session.AdvanceConcurrencyVersion();
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return Success(runtimeContext with { });
            },
            cancellationToken
        );

    /// <inheritdoc />
    public Task<IResult<RuntimeProfilingSnapshot>> AddSnapshotAsync(
        RuntimeProfilingSnapshot snapshot,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var resolved = await ResolveSessionNodeReferenceAsync(
                        context,
                        snapshot?.SessionId ?? Guid.Empty,
                        snapshot?.SessionKey,
                        snapshot?.NodeId ?? Guid.Empty,
                        snapshot?.NodeKey,
                        token
                    )
                    .ConfigureAwait(false);
                if (resolved.Error is not null)
                {
                    return Failure<RuntimeProfilingSnapshot>(resolved.Error);
                }

                if (
                    snapshot.TimestampUtc < resolved.Session.StartedUtc
                    || snapshot.TimestampUtc > resolved.Session.EndsUtc
                )
                {
                    return Failure<RuntimeProfilingSnapshot>(
                        new ProfilingInvalidStateError(
                            "The snapshot timestamp is outside the session collection window."
                        )
                    );
                }

                if (snapshot.Sequence <= 0)
                {
                    return Failure<RuntimeProfilingSnapshot>(
                        new ProfilingValidationError("Snapshot sequence must be greater than zero.")
                    );
                }

                var existing = await context
                    .Set<RuntimeProfilingSnapshotEntity>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == snapshot.Identity.Id, token)
                    .ConfigureAwait(false);
                if (existing is not null)
                {
                    var model = ProfilingEntityMapper.ToModel(
                        existing,
                        resolved.Session.Key,
                        resolved.Node.Key
                    );
                    return model == snapshot
                        ? Success(model)
                        : Failure<RuntimeProfilingSnapshot>(
                            new ProfilingInvalidStateError("A stored snapshot cannot be changed.")
                        );
                }

                if (
                    await context
                        .Set<RuntimeProfilingSnapshotEntity>().AnyAsync(
                            x =>
                                x.Key == snapshot.Identity.Key
                                || x.SessionId == snapshot.SessionId
                                    && x.NodeId == snapshot.NodeId
                                    && x.Sequence == snapshot.Sequence,
                            token
                        )
                        .ConfigureAwait(false)
                )
                {
                    return Failure<RuntimeProfilingSnapshot>(
                        new ProfilingValidationError(
                            "The snapshot key or node-local sequence is already in use."
                        )
                    );
                }

                context.Set<RuntimeProfilingSnapshotEntity>().Add(ProfilingEntityMapper.ToEntity(snapshot));
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return Success(snapshot);
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<IResult<ProfilingMarker>> AddMarkerAsync(
        ProfilingMarker marker,
        CancellationToken cancellationToken = default
    )
    {
        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        var session = marker is null
                            ? null
                            : await context
                                .Set<RuntimeProfilingSessionEntity>().SingleOrDefaultAsync(
                                    x => x.Id == marker.SessionId && x.Key == marker.SessionKey,
                                    token
                                )
                                .ConfigureAwait(false);
                        if (session is null)
                        {
                            return Failure<ProfilingMarker>(
                                new NotFoundError("The active profiling session was not found.")
                            );
                        }

                        if (
                            session.State != RuntimeProfilingSessionState.Running
                            || marker.TimestampUtc < session.StartedUtc
                            || marker.TimestampUtc > session.EndsUtc
                        )
                        {
                            return Failure<ProfilingMarker>(
                                new ProfilingInvalidStateError(
                                    "A marker requires an active session and a timestamp inside its collection window."
                                )
                            );
                        }

                        if (
                            string.IsNullOrWhiteSpace(marker.Name)
                            || marker.Name.Trim().Length > 100
                        )
                        {
                            return Failure<ProfilingMarker>(
                                new ProfilingValidationError(
                                    "A marker name must contain 1 to 100 characters."
                                )
                            );
                        }

                        if (!Enum.IsDefined(marker.Scope) || string.IsNullOrWhiteSpace(marker.Kind) || marker.Kind.Length > 128
                            || marker.Scope == ProfilingMarkerScope.Session && (marker.NodeId is not null || marker.NodeKey is not null))
                        {
                            return Failure<ProfilingMarker>(new ProfilingValidationError("A bounded marker kind and valid scope are required."));
                        }

                        if (marker.Scope == ProfilingMarkerScope.Node)
                        {
                            var reference = await ResolveSessionNodeReferenceAsync(context, marker.SessionId, marker.SessionKey, marker.NodeId ?? Guid.Empty, marker.NodeKey, token).ConfigureAwait(false);
                            if (reference.Error is not null)
                            {
                                return Failure<ProfilingMarker>(reference.Error);
                            }
                        }

                        var normalized = marker with { Name = marker.Name.Trim() };
                        var existing = session.Markers.SingleOrDefault(x => x.Id == marker.Id);
                        if (existing is not null)
                        {
                            var model = ProfilingEntityMapper.ToModel(existing, session.Key, normalized.NodeKey);
                            return model == normalized
                                ? Success(model)
                                : Failure<ProfilingMarker>(
                                    new ProfilingInvalidStateError(
                                        "A stored marker cannot be changed."
                                    )
                                );
                        }

                        session.Markers.Add(ProfilingEntityMapper.ToEntity(normalized));
                        session.AdvanceConcurrencyVersion();
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        return Success(normalized);
                    },
                    cancellationToken,
                    transactional: true
                )
                .ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public Task<IResult<ProfilingSegment>> UpsertSegmentAsync(
        ProfilingSegment segment,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var resolved = await ResolveSessionNodeAsync(
                        context,
                        segment?.SessionId ?? Guid.Empty,
                        segment?.SessionKey,
                        segment?.NodeId ?? Guid.Empty,
                        segment?.NodeKey,
                        token
                    )
                    .ConfigureAwait(false);
                if (resolved.Error is not null)
                {
                    return Failure<ProfilingSegment>(resolved.Error);
                }

                if (segment.Id == Guid.Empty || string.IsNullOrWhiteSpace(segment.Name))
                {
                    return Failure<ProfilingSegment>(
                        new ProfilingValidationError("A segment identity and name are required.")
                    );
                }

                if (segment.ParentSegmentId is { } parentId)
                {
                    var parent = resolved.Session.Segments.SingleOrDefault(x => x.Id == parentId);
                    if (
                        parent is null
                        || parent.SessionId != segment.SessionId
                        || parent.NodeId != segment.NodeId
                    )
                    {
                        return Failure<ProfilingSegment>(
                            new ProfilingValidationError(
                                "A parent segment must belong to the same session and node."
                            )
                        );
                    }
                }

                var normalized = segment with
                {
                    Name = segment.Name.Trim(),
                    Tags = NormalizeStrings(segment.Tags),
                };
                var entity = resolved.Session.Segments.SingleOrDefault(x => x.Id == segment.Id);
                if (entity is null)
                {
                    if (
                        resolved.Session.State != RuntimeProfilingSessionState.Running
                        || segment.StartedUtc < resolved.Session.StartedUtc
                        || segment.StartedUtc > resolved.Session.EndsUtc
                        || segment.Outcome != null
                    )
                    {
                        return Failure<ProfilingSegment>(
                            new ProfilingInvalidStateError(
                                "A new segment must open inside an active session collection window."
                            )
                        );
                    }

                    entity = ProfilingEntityMapper.ToEntity(normalized);
                    resolved.Session.Segments.Add(entity);
                    resolved.Session.AdvanceConcurrencyVersion();
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                    return Success(
                        ProfilingEntityMapper.ToModel(
                            entity,
                            resolved.Session.Key,
                            resolved.Node.Key
                        )
                    );
                }

                var existing = ProfilingEntityMapper.ToModel(
                    entity,
                    resolved.Session.Key,
                    resolved.Node.Key
                );
                if (SegmentEquals(existing, normalized))
                {
                    return Success(existing);
                }

                if (
                    entity.Outcome != null
                    || normalized.Outcome == null
                    || entity.SessionId != normalized.SessionId
                    || entity.NodeId != normalized.NodeId
                    || entity.StartedUtc != normalized.StartedUtc
                    || !string.Equals(entity.Name, normalized.Name, StringComparison.Ordinal)
                    || normalized.EndedUtc < normalized.StartedUtc
                    || normalized.Elapsed < TimeSpan.Zero
                )
                {
                    return Failure<ProfilingSegment>(
                        new ProfilingInvalidStateError(
                            "A segment can only transition once from open to a terminal outcome."
                        )
                    );
                }

                ReplaceItems(
                    entity.Tags,
                    ProfilingEntityMapper.ToSegmentTags(entity.Id, normalized.Tags)
                );
                entity.EndedUtc = normalized.EndedUtc;
                entity.Elapsed = normalized.Elapsed;
                entity.Outcome = normalized.Outcome;
                entity.ExceptionType = NormalizeOptional(normalized.ExceptionType);
                entity.ExceptionMessage = NormalizeOptional(normalized.ExceptionMessage);
                entity.Note = NormalizeOptional(normalized.Note);
                entity.CorrelationId = NormalizeOptional(normalized.CorrelationId);
                entity.ParentSegmentId = normalized.ParentSegmentId;
                entity.CollectionEndedBeforeOperation = normalized.CollectionEndedBeforeOperation;
                resolved.Session.AdvanceConcurrencyVersion();
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return Success(
                    ProfilingEntityMapper.ToModel(entity, resolved.Session.Key, resolved.Node.Key)
                );
            },
            cancellationToken,
            transactional: true
        );

    /// <inheritdoc />
    public Task<IResult<ProfilingMetricObservation>> AddMetricObservationAsync(
        ProfilingMetricObservation observation,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var resolved = await ResolveSessionNodeReferenceAsync(
                        context,
                        observation?.SessionId ?? Guid.Empty,
                        observation?.SessionKey,
                        observation?.NodeId ?? Guid.Empty,
                        observation?.NodeKey,
                        token
                    )
                    .ConfigureAwait(false);
                if (resolved.Error is not null)
                {
                    return Failure<ProfilingMetricObservation>(resolved.Error);
                }

                if (
                    observation.Id == Guid.Empty
                    || string.IsNullOrWhiteSpace(observation.MetricIdentifier)
                    || observation.TimestampUtc < resolved.Session.StartedUtc
                    || observation.TimestampUtc > resolved.Session.EndsUtc
                )
                {
                    return Failure<ProfilingMetricObservation>(
                        new ProfilingValidationError(
                            "A metric identity, stable identifier, and timestamp inside the collection window are required."
                        )
                    );
                }

                if (observation.SegmentId is { } segmentId)
                {
                    var sessionAggregate = await context
                        .Set<RuntimeProfilingSessionEntity>().AsNoTracking()
                        .SingleAsync(x => x.Id == observation.SessionId, token)
                        .ConfigureAwait(false);
                    var segment = sessionAggregate.Segments.SingleOrDefault(x => x.Id == segmentId);
                    if (
                        segment is null
                        || segment.SessionId != observation.SessionId
                        || segment.NodeId != observation.NodeId
                    )
                    {
                        return Failure<ProfilingMetricObservation>(
                            new ProfilingValidationError(
                                "An ambient metric segment must belong to the same session and node."
                            )
                        );
                    }
                }

                var normalized = observation with
                {
                    MetricIdentifier = observation.MetricIdentifier.Trim(),
                    Unit = NormalizeOptional(observation.Unit),
                };
                var existing = await context
                    .Set<RuntimeProfilingMetricObservationEntity>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == observation.Id, token)
                    .ConfigureAwait(false);
                if (existing is not null)
                {
                    var model = ProfilingEntityMapper.ToModel(
                        existing,
                        resolved.Session.Key,
                        resolved.Node.Key
                    );
                    return model == normalized
                        ? Success(model)
                        : Failure<ProfilingMetricObservation>(
                            new ProfilingInvalidStateError(
                                "A stored metric observation cannot be changed."
                            )
                        );
                }

                context.Set<RuntimeProfilingMetricObservationEntity>().Add(ProfilingEntityMapper.ToEntity(normalized));
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return Success(normalized);
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingSessionData>> GetSessionDataAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsPublicKey(sessionKey))
        {
            return Failure<RuntimeProfilingSessionData>(
                new NotFoundError($"Profiling session '{sessionKey}' was not found.")
            );
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var session = await context
            .Set<RuntimeProfilingSessionEntity>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == sessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            return Failure<RuntimeProfilingSessionData>(
                new NotFoundError($"Profiling session '{sessionKey}' was not found.")
            );
        }

        var participations = await context
            .Set<RuntimeProfilingParticipationEntity>().AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var snapshots = await context
            .Set<RuntimeProfilingSnapshotEntity>().AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var observations = await context
            .Set<RuntimeProfilingMetricObservationEntity>().AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var nodeIds = participations
            .Select(x => x.NodeId)
            .Concat(session.RuntimeContexts.Select(x => x.NodeId))
            .Concat(snapshots.Select(x => x.NodeId))
            .Concat(session.Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Node).Select(x => x.NodeId.Value))
            .Concat(session.Segments.Select(x => x.NodeId))
            .Concat(observations.Select(x => x.NodeId))
            .Distinct()
            .ToArray();
        var nodes = await context
            .Set<ProfilingNodeEntity>().AsNoTracking()
            .Where(x => nodeIds.Contains(x.Id))
            .OrderBy(x => x.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var nodeKeys = nodes.ToDictionary(x => x.Id, x => x.Key);

        return Success(
            new RuntimeProfilingSessionData
            {
                Session = ProfilingEntityMapper.ToModel(session),
                Participations = participations
                    .OrderBy(x => nodeKeys.GetValueOrDefault(x.NodeId), StringComparer.Ordinal)
                    .Select(x =>
                        ProfilingEntityMapper.ToModel(
                            x,
                            session.Key,
                            nodeKeys.GetValueOrDefault(x.NodeId)
                        )
                    )
                    .ToArray(),
                Nodes = nodes.Select(ProfilingEntityMapper.ToModel).ToArray(),
                RuntimeContexts = session
                    .RuntimeContexts.Select(x =>
                        ProfilingEntityMapper.ToModel(
                            x,
                            session.Key,
                            nodeKeys.GetValueOrDefault(x.NodeId)
                        )
                    )
                    .ToArray(),
                Snapshots = snapshots
                    .OrderBy(x => x.TimestampUtc)
                    .ThenBy(x => x.NodeId)
                    .ThenBy(x => x.Sequence)
                    .Select(x =>
                        ProfilingEntityMapper.ToModel(
                            x,
                            session.Key,
                            nodeKeys.GetValueOrDefault(x.NodeId)
                        )
                    )
                    .ToArray(),
                Markers = [ ..(session
                    .Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Session).OrderBy(x => x.TimestampUtc)
                    .Select(x => ProfilingEntityMapper.ToModel(x, session.Key))
                    .ToArray()), ..(session
                    .Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Node).OrderBy(x => x.TimestampUtc)
                    .Select(x =>
                        ProfilingEntityMapper.ToModel(
                            x,
                            session.Key,
                            nodeKeys.GetValueOrDefault(x.NodeId ?? Guid.Empty)
                        )
                    )
                    .ToArray()) ],
                Segments = session
                    .Segments.OrderBy(x => x.StartedUtc)
                    .Select(x =>
                        ProfilingEntityMapper.ToModel(
                            x,
                            session.Key,
                            nodeKeys.GetValueOrDefault(x.NodeId)
                        )
                    )
                    .ToArray(),
                MetricObservations = observations
                    .OrderBy(x => x.TimestampUtc)
                    .Select(x =>
                        ProfilingEntityMapper.ToModel(
                            x,
                            session.Key,
                            nodeKeys.GetValueOrDefault(x.NodeId)
                        )
                    )
                    .ToArray(),
            }
        );
    }

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingSession>> ImportSessionAsync(
        RuntimeProfilingSessionData data,
        CancellationToken cancellationToken = default
    )
    {
        var validation = ValidateImportData(data);
        if (validation is not null)
        {
            return Failure<RuntimeProfilingSession>(validation);
        }

        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        var maintenance = await EntityFrameworkProfilingRuntimeGate.AcquireAsync(context, token).ConfigureAwait(false);
                        if (maintenance.MaintenanceClearId is not null)
                        {
                            return Failure<RuntimeProfilingSession>(new ProfilingBusyError("Runtime history is being cleared."));
                        }

                        var sessionId = data.Session.Identity.Id;
                        var sessionKey = data.Session.Identity.Key;
                        var nodeIds = data.Nodes.Select(item => item.Identity.Id).ToArray();
                        var nodeKeys = data.Nodes.Select(item => item.Identity.Key).ToArray();
                        var snapshotIds = data.Snapshots
                            .Select(item => item.Identity.Id)
                            .ToArray();
                        var snapshotKeys = data.Snapshots
                            .Select(item => item.Identity.Key)
                            .ToArray();
                        var correlations = data.Nodes
                            .Select(item => item.Correlation)
                            .ToArray();

                        var collision =
                            await context.Set<RuntimeProfilingSessionEntity>().AnyAsync(
                                item => item.Id == sessionId || item.Key == sessionKey,
                                token
                            ).ConfigureAwait(false)
                            || await context.Set<RuntimeProfilingInvalidSessionEntity>().AnyAsync(
                                item => item.Id == sessionId || item.Key == sessionKey,
                                token
                            ).ConfigureAwait(false)
                            || await context.Set<ProfilingNodeEntity>().AnyAsync(
                                item =>
                                    nodeIds.Contains(item.Id)
                                    || nodeKeys.Contains(item.Key),
                                token
                            ).ConfigureAwait(false)
                            || await context.Set<RuntimeProfilingSnapshotEntity>().AnyAsync(
                                item =>
                                    snapshotIds.Contains(item.Id)
                                    || snapshotKeys.Contains(item.Key),
                                token
                            ).ConfigureAwait(false);
                        if (!collision)
                        {
                            foreach (var correlation in correlations)
                            {
                                collision = await context.Set<ProfilingNodeEntity>().AnyAsync(
                                    item =>
                                        item.BroadcastNodeIdentity
                                            == correlation.BroadcastNodeIdentity
                                        && item.ProcessStartedUtc == correlation.ProcessStartedUtc,
                                    token
                                ).ConfigureAwait(false);
                                if (collision)
                                {
                                    break;
                                }
                            }
                        }

                        if (collision)
                        {
                            return Failure<RuntimeProfilingSession>(
                                new ProfilingValidationError(
                                    "An imported Profiling identity is already in use."
                                )
                            );
                        }

                        var session = ProfilingEntityMapper.ToEntity(data.Session);
                        session.RuntimeContexts = data.RuntimeContexts
                            .Select(ProfilingEntityMapper.ToEntity)
                            .ToArray();
                        session.Markers = data.Markers.Select(ProfilingEntityMapper.ToEntity).ToArray();
                        session.Segments = data.Segments
                            .Select(ProfilingEntityMapper.ToEntity)
                            .ToArray();

                        context.Set<ProfilingNodeEntity>().AddRange(
                            data.Nodes.Select(item =>
                                ProfilingEntityMapper.ToEntity(item.Correlation, item)
                            )
                        );
                        context.Set<RuntimeProfilingSessionEntity>().Add(session);
                        context.Set<RuntimeProfilingParticipationEntity>().AddRange(
                            data.Participations.Select(ProfilingEntityMapper.ToEntity)
                        );
                        context.Set<RuntimeProfilingSnapshotEntity>().AddRange(
                            data.Snapshots.Select(ProfilingEntityMapper.ToEntity)
                        );
                        context.Set<RuntimeProfilingMetricObservationEntity>().AddRange(
                            data.MetricObservations.Select(ProfilingEntityMapper.ToEntity)
                        );
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        return Success(ProfilingEntityMapper.ToModel(session));
                    },
                    cancellationToken,
                    transactional: true
                )
                .ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IResult<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        var entity = await context
                            .Set<RuntimeProfilingSessionEntity>().SingleOrDefaultAsync(x => x.Key == sessionKey, token)
                            .ConfigureAwait(false);
                        if (entity is null)
                        {
                            return Failure<bool>(
                                new NotFoundError(
                                    $"Profiling session '{sessionKey}' was not found."
                                )
                            );
                        }

                        if (entity.State == RuntimeProfilingSessionState.Running)
                        {
                            return Failure<bool>(
                                new ProfilingInvalidStateError(
                                    "An active profiling session must be stopped before deletion."
                                )
                            );
                        }

                        await AddTombstoneAsync(context, entity, token).ConfigureAwait(false);
                        context.Set<RuntimeProfilingSessionEntity>().Remove(entity);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        return Success(true);
                    },
                    cancellationToken,
                    transactional: true
                )
                .ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IResult<int>> DeleteUnpinnedSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.DeleteSessionsAsync(
                    context =>
                        context.Set<RuntimeProfilingSessionEntity>().Where(x =>
                            x.State != RuntimeProfilingSessionState.Running && !x.IsPinned
                        ),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IResult<ProfilingClearResult>> ClearAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (this.SharedClear is not null)
        {
            return await this.SharedClear(cancellationToken).ConfigureAwait(false);
        }

        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        if (
                            await context
                                .Set<RuntimeProfilingSessionEntity>().AnyAsync(
                                    x => x.State == RuntimeProfilingSessionState.Running,
                                    token
                                )
                                .ConfigureAwait(false)
                        )
                        {
                            return Failure<ProfilingClearResult>(
                                new ProfilingInvalidStateError(
                                    "The active profiling session must be stopped before clearing the store."
                                )
                            );
                        }

                        var sessions = await context
                            .Set<RuntimeProfilingSessionEntity>().ToListAsync(token)
                            .ConfigureAwait(false);
                        var snapshotCount = await context
                            .Set<RuntimeProfilingSnapshotEntity>().LongCountAsync(token)
                            .ConfigureAwait(false);
                        foreach (var session in sessions)
                        {
                            await AddTombstoneAsync(context, session, token).ConfigureAwait(false);
                        }

                        context.Set<RuntimeProfilingSessionEntity>().RemoveRange(sessions);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);

                        var nodes = await context
                            .Set<ProfilingNodeEntity>().ToListAsync(token)
                            .ConfigureAwait(false);
                        context.Set<ProfilingNodeEntity>().RemoveRange(nodes);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        return Success(new ProfilingClearResult(sessions.Count, snapshotCount));
                    },
                    cancellationToken,
                    transactional: true
                )
                .ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IResult<int>> ApplyRetentionAsync(
        int maximumRetainedSessions,
        TimeSpan maximumSessionAge,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default
    )
    {
        if (maximumRetainedSessions <= 0 || maximumSessionAge <= TimeSpan.Zero)
        {
            return Failure<int>(
                new ProfilingValidationError(
                    "Retention requires a positive session count and maximum age."
                )
            );
        }

        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        var terminal = await context
                            .Set<RuntimeProfilingSessionEntity>().Where(x =>
                                x.State != RuntimeProfilingSessionState.Running && !x.IsPinned
                            )
                            .ToListAsync(token)
                            .ConfigureAwait(false);
                        var threshold = utcNow.Subtract(maximumSessionAge);
                        var candidates = terminal
                            .OrderByDescending(x => x.CompletedUtc ?? x.EndsUtc)
                            .Where(
                                (session, index) =>
                                    (session.CompletedUtc ?? session.EndsUtc) < threshold
                                    || index >= maximumRetainedSessions
                            )
                            .DistinctBy(x => x.Id)
                            .ToArray();
                        foreach (var session in candidates)
                        {
                            await AddTombstoneAsync(context, session, token).ConfigureAwait(false);
                        }

                        context.Set<RuntimeProfilingSessionEntity>().RemoveRange(candidates);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        return Success(candidates.Length);
                    },
                    cancellationToken,
                    transactional: true
                )
                .ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    private async Task<IResult<int>> DeleteSessionsAsync(
        Func<TContext, IQueryable<RuntimeProfilingSessionEntity>> query,
        CancellationToken cancellationToken
    ) =>
        await this.ExecuteWriteAsync(
                async (context, token) =>
                {
                    var sessions = await query(context).ToListAsync(token).ConfigureAwait(false);
                    foreach (var session in sessions)
                    {
                        await AddTombstoneAsync(context, session, token).ConfigureAwait(false);
                    }

                    context.Set<RuntimeProfilingSessionEntity>().RemoveRange(sessions);
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                    return Success(sessions.Count);
                },
                cancellationToken,
                transactional: true
            )
            .ConfigureAwait(false);

    private async Task<IResult<T>> ExecuteWriteAsync<T>(
        Func<TContext, CancellationToken, Task<Result<T>>> action,
        CancellationToken cancellationToken,
        bool transactional = false
    )
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            try
            {
                await using var transaction = await BeginTransactionAsync(
                        context,
                        transactional,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                var result = await action(context, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess && transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return result;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                // Retry once with a fresh operation-owned context.
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // A competing insert may have won a unique lifecycle or identity key.
            }
            catch (DbException) when (attempt == 0)
            {
                // A relational serialization or lock conflict may be retried once.
            }
            catch (InvalidOperationException exception)
                when (attempt == 0 && ContainsDatabaseConflict(exception))
            {
                // Provider execution strategies may wrap the database conflict.
            }
            catch (DbUpdateConcurrencyException)
            {
                return Failure<T>(
                    new ProfilingInvalidStateError(
                        "The profiling store changed concurrently; retry the operation."
                    )
                );
            }
            catch (DbUpdateException exception)
            {
                return Failure<T>(
                    new ProfilingUnavailableError(
                        $"The Entity Framework profiling store could not commit: {exception.GetType().Name}."
                    )
                );
            }
            catch (DbException exception)
            {
                return Failure<T>(
                    new ProfilingUnavailableError(
                        $"The Entity Framework profiling transaction could not complete: {exception.GetType().Name}."
                    )
                );
            }
            catch (InvalidOperationException exception) when (ContainsDatabaseConflict(exception))
            {
                return Failure<T>(
                    new ProfilingUnavailableError(
                        $"The Entity Framework profiling transaction could not complete: {exception.GetType().Name}."
                    )
                );
            }
        }

        return Failure<T>(
            new ProfilingInvalidStateError(
                "The profiling store changed concurrently; retry the operation."
            )
        );
    }

    private static IResultError ValidateImportData(RuntimeProfilingSessionData data)
    {
        var session = data?.Session;
        if (
            session is null
            || session.Identity.Id == Guid.Empty
            || !IsPublicKey(session.Identity.Key)
            || !IsTerminal(session.State)
            || data.Nodes is null
            || data.Participations is null
            || data.RuntimeContexts is null
            || data.Snapshots is null
            || data.Markers is null
            || data.Segments is null
            || data.MetricObservations is null
        )
        {
            return new ProfilingValidationError(
                "A complete terminal Profiling session graph is required for import."
            );
        }

        if (data.Nodes.Any(item => item is null))
        {
            return new ProfilingValidationError("Imported Profiling nodes cannot be null.");
        }

        if (data.Markers is null || data.Markers.Any(marker => marker is null
            || !Enum.IsDefined(marker.Scope) || string.IsNullOrWhiteSpace(marker.Kind) || marker.Kind.Length > 128
            || string.IsNullOrWhiteSpace(marker.Name) || marker.Name.Trim().Length > 100
            || marker.TimestampUtc.Offset != TimeSpan.Zero || marker.TimestampUtc < session.StartedUtc || marker.TimestampUtc > session.EndsUtc
            || marker.Scope == ProfilingMarkerScope.Session && (marker.NodeId is not null || marker.NodeKey is not null)
            || marker.Scope == ProfilingMarkerScope.Node && (marker.NodeId is null || string.IsNullOrWhiteSpace(marker.NodeKey)))
            || data.Markers.Select(marker => marker.Id).Distinct().Count() != data.Markers.Count)
        {
            return new ProfilingValidationError("Imported markers must have unique identities, bounded labels and valid explicit scopes.");
        }

        var nodeIds = data.Nodes.Select(item => item.Identity.Id).ToHashSet();
        var nodeKeys = data.Nodes.ToDictionary(item => item.Identity.Id, item => item.Identity.Key);
        if (
            nodeIds.Contains(Guid.Empty)
            || data.Nodes.Any(item =>
                item is null
                || !IsPublicKey(item.Identity.Key)
                || item.Correlation is null
            )
            || nodeIds.Count != data.Nodes.Count
            || nodeKeys.Values.Distinct(StringComparer.Ordinal).Count() != data.Nodes.Count
        )
        {
            return new ProfilingValidationError(
                "Imported Profiling nodes must have unique identities and correlations."
            );
        }

        bool ValidSession(Guid id, string key) =>
            id == session.Identity.Id && string.Equals(key, session.Identity.Key, StringComparison.Ordinal);
        bool ValidNode(Guid id, string key) =>
            nodeIds.Contains(id)
            && nodeKeys.TryGetValue(id, out var expected)
            && string.Equals(key, expected, StringComparison.Ordinal);

        if (
            data.Participations.Any(item =>
                item is null
                || !ValidSession(item.SessionId, item.SessionKey)
                || !ValidNode(item.NodeId, item.NodeKey)
            )
            || data.RuntimeContexts.Any(item =>
                item is null
                || !ValidSession(item.SessionId, item.SessionKey)
                || !ValidNode(item.NodeId, item.NodeKey)
            )
            || data.Snapshots.Any(item =>
                item is null
                || item.Identity.Id == Guid.Empty
                || !IsPublicKey(item.Identity.Key)
                || !ValidSession(item.SessionId, item.SessionKey)
                || !ValidNode(item.NodeId, item.NodeKey)
            )
            || data.Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Session).Any(item =>
                item is null || item.Id == Guid.Empty || !ValidSession(item.SessionId, item.SessionKey)
            )
            || data.Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Node).Any(item =>
                item is null
                || item.Id == Guid.Empty
                || !ValidSession(item.SessionId, item.SessionKey)
                || !ValidNode(item.NodeId ?? Guid.Empty, item.NodeKey)
            )
            || data.Segments.Any(item =>
                item is null
                || item.Id == Guid.Empty
                || !ValidSession(item.SessionId, item.SessionKey)
                || !ValidNode(item.NodeId, item.NodeKey)
            )
            || data.MetricObservations.Any(item =>
                item is null
                || item.Id == Guid.Empty
                || !ValidSession(item.SessionId, item.SessionKey)
                || !ValidNode(item.NodeId, item.NodeKey)
            )
        )
        {
            return new ProfilingValidationError(
                "The imported Profiling graph contains invalid identities or relationships."
            );
        }

        var segmentNodes = data.Segments.ToDictionary(item => item.Id, item => item.NodeId);
        if (
            data.Participations.Select(item => item.NodeId).Distinct().Count()
                != data.Participations.Count
            || data.RuntimeContexts.Select(item => item.NodeId).Distinct().Count()
                != data.RuntimeContexts.Count
            || data.Snapshots.Select(item => item.Identity.Id).Distinct().Count()
                != data.Snapshots.Count
            || data.Snapshots.Select(item => item.Identity.Key).Distinct(StringComparer.Ordinal).Count()
                != data.Snapshots.Count
            || segmentNodes.Count != data.Segments.Count
            || data.Segments.Any(item =>
                item.ParentSegmentId is { } parent
                && (!segmentNodes.TryGetValue(parent, out var nodeId) || nodeId != item.NodeId)
            )
            || data.MetricObservations.Any(item =>
                item.SegmentId is { } segment
                && (!segmentNodes.TryGetValue(segment, out var nodeId) || nodeId != item.NodeId)
            )
        )
        {
            return new ProfilingValidationError(
                "The imported Profiling graph contains duplicate or inconsistent relationships."
            );
        }

        return null;
    }

    private static async Task<IDbContextTransaction> BeginTransactionAsync(
        TContext context,
        bool transactional,
        CancellationToken cancellationToken
    ) =>
        transactional && context.Database.IsRelational()
            ? await context
                .Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false)
            : null;

    private static async Task AddTombstoneAsync(
        TContext context,
        RuntimeProfilingSessionEntity session,
        CancellationToken cancellationToken
    )
    {
        var gate = context.Set<ProfilingRuntimeGateEntity>().Local.SingleOrDefault(row => row.Id == 1)
            ?? await EntityFrameworkProfilingRuntimeGate.AcquireAsync(context, cancellationToken).ConfigureAwait(false);
        gate.DeletionRevision = checked(gate.DeletionRevision + 1);
        if (
            !await context
                .Set<RuntimeProfilingInvalidSessionEntity>().AnyAsync(
                    x => x.Id == session.Id || x.Key == session.Key,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            context.Set<RuntimeProfilingInvalidSessionEntity>().Add(
                new RuntimeProfilingInvalidSessionEntity { Id = session.Id, Key = session.Key }
            );
        }
    }

    private static async Task<SessionNodeResolution> ResolveSessionNodeAsync(
        TContext context,
        Guid sessionId,
        string sessionKey,
        Guid nodeId,
        string nodeKey,
        CancellationToken cancellationToken
    )
    {
        var session = await context
            .Set<RuntimeProfilingSessionEntity>().SingleOrDefaultAsync(
                x => x.Id == sessionId && x.Key == sessionKey,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (session is null)
        {
            return new SessionNodeResolution(
                null,
                null,
                new NotFoundError("The profiling session was not found.")
            );
        }

        var node = await context
            .Set<ProfilingNodeEntity>().SingleOrDefaultAsync(
                x => x.Id == nodeId && x.Key == nodeKey,
                cancellationToken
            )
            .ConfigureAwait(false);
        return node is null
            ? new SessionNodeResolution(
                session,
                null,
                new NotFoundError("The profiling node was not found.")
            )
            : new SessionNodeResolution(session, node, null);
    }

    private static async Task<SessionNodeReferenceResolution> ResolveSessionNodeReferenceAsync(
        TContext context,
        Guid sessionId,
        string sessionKey,
        Guid nodeId,
        string nodeKey,
        CancellationToken cancellationToken
    )
    {
        var session = await context
            .Set<RuntimeProfilingSessionEntity>().AsNoTracking()
            .Where(x => x.Id == sessionId && x.Key == sessionKey)
            .Select(x => new SessionReference(x.Id, x.Key, x.StartedUtc, x.EndsUtc))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            return new SessionNodeReferenceResolution(
                null,
                null,
                new NotFoundError("The profiling session was not found.")
            );
        }

        var node = await context
            .Set<ProfilingNodeEntity>().AsNoTracking()
            .Where(x => x.Id == nodeId && x.Key == nodeKey)
            .Select(x => new NodeReference(x.Id, x.Key))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return node is null
            ? new SessionNodeReferenceResolution(
                session,
                null,
                new NotFoundError("The profiling node was not found.")
            )
            : new SessionNodeReferenceResolution(session, node, null);
    }

    private static IResultError ValidateSessionRequest(RuntimeProfilingSessionCreateRequest request)
    {
        if (request is null)
        {
            return new ProfilingValidationError("A profiling session request is required.");
        }

        if (
            request.Identity.Id == Guid.Empty
            || !IsPublicKey(request.Identity.Key)
            || request.SamplingInterval < RuntimeProfilingOptions.MinimumSamplingInterval
            || request.Duration <= TimeSpan.Zero
        )
        {
            return new ProfilingValidationError(
                "A valid session identity, sampling interval, and positive duration are required."
            );
        }

        try
        {
            _ = request.StartedUtc.Add(request.Duration);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new ProfilingValidationError("The session end timestamp is out of range.");
        }

        return null;
    }

    private static IResultError ValidateNode(
        RuntimeProfilingNodeCorrelation correlation,
        ProfilingNode proposedNode
    )
    {
        if (
            correlation is null
            || string.IsNullOrWhiteSpace(correlation.BroadcastNodeIdentity)
            || proposedNode is null
            || proposedNode.Identity.Id == Guid.Empty
            || !IsPublicKey(proposedNode.Identity.Key)
            || proposedNode.ProcessId <= 0
        )
        {
            return new ProfilingValidationError(
                "A valid Broadcast correlation and proposed profiling node are required."
            );
        }

        return proposedNode.Correlation is not null && proposedNode.Correlation != correlation
            ? new ProfilingValidationError(
                "The proposed node correlation does not match the requested Broadcast registration."
            )
            : null;
    }

    private static bool SegmentEquals(ProfilingSegment left, ProfilingSegment right) =>
        left.Id == right.Id
        && left.SessionId == right.SessionId
        && left.SessionKey == right.SessionKey
        && left.NodeId == right.NodeId
        && left.NodeKey == right.NodeKey
        && left.Name == right.Name
        && left.StartedUtc == right.StartedUtc
        && left.EndedUtc == right.EndedUtc
        && left.Elapsed == right.Elapsed
        && left.Outcome == right.Outcome
        && left.ExceptionType == right.ExceptionType
        && left.ExceptionMessage == right.ExceptionMessage
        && left.Note == right.Note
        && left.CorrelationId == right.CorrelationId
        && left.ParentSegmentId == right.ParentSegmentId
        && left.CollectionEndedBeforeOperation == right.CollectionEndedBeforeOperation
        && left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal);

    private static bool IsValidTransition(
        RuntimeProfilingSessionState current,
        RuntimeProfilingSessionState next
    ) => current == next || current == RuntimeProfilingSessionState.Running && IsTerminal(next);

    private static bool IsTerminal(RuntimeProfilingSessionState state) =>
        state
            is RuntimeProfilingSessionState.Completed
                or RuntimeProfilingSessionState.CompletedWithWarnings
                or RuntimeProfilingSessionState.Stopped
                or RuntimeProfilingSessionState.Failed;

    private static bool IsTerminal(RuntimeProfilingParticipationState state) =>
        state
            is RuntimeProfilingParticipationState.Completed
                or RuntimeProfilingParticipationState.Stopped
                or RuntimeProfilingParticipationState.Failed;

    private static int ParticipationRank(RuntimeProfilingParticipationState state) =>
        state switch
        {
            RuntimeProfilingParticipationState.Accepted => 0,
            RuntimeProfilingParticipationState.Collecting => 1,
            _ => 2,
        };

    private static bool IsPublicKey(string value) =>
        value?.Length == 8
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9');

    private static bool ContainsDatabaseConflict(Exception exception) =>
        exception is DbUpdateException or DbException
        || exception.InnerException is not null
            && ContainsDatabaseConflict(exception.InnerException);

    private static string NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> NormalizeStrings(IEnumerable<string> values) =>
        values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() ?? [];

    private static void ReplaceItems<T>(ICollection<T> target, IEnumerable<T> replacement)
    {
        target.Clear();
        foreach (var item in replacement)
        {
            target.Add(item);
        }
    }

    private static Result<T> Success<T>(T value) => Result<T>.Success(value);

    private static Result<T> Failure<T>(IResultError error) => Result<T>.Failure().WithError(error);

    private sealed record SessionNodeResolution(
        RuntimeProfilingSessionEntity Session,
        ProfilingNodeEntity Node,
        IResultError Error
    );

    private sealed record SessionNodeReferenceResolution(
        SessionReference Session,
        NodeReference Node,
        IResultError Error
    );

    private sealed record SessionReference(
        Guid Id,
        string Key,
        DateTimeOffset StartedUtc,
        DateTimeOffset EndsUtc
    );

    private sealed record NodeReference(Guid Id, string Key);
}
