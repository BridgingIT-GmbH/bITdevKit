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
/// The application context implementing <see cref="IProfilingContext"/>.
/// </typeparam>
/// <remarks>
/// The singleton provider never retains a scoped context. Every operation owns a fresh dependency
/// injection scope and context, while relational lifecycle mutations use serializable transactions.
/// </remarks>
/// <example>
/// <code>
/// services.AddProfiling(options => options.Enabled())
///     .WithEntityFrameworkStore&lt;AppDbContext&gt;();
/// </code>
/// </example>
public sealed class EntityFrameworkProfilingStore<TContext>(IServiceScopeFactory scopeFactory)
    : IProfilingStore
    where TContext : DbContext, IProfilingContext
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);

    /// <inheritdoc />
    public ProfilingStoreCapabilities Capabilities { get; } = new(true);

    /// <inheritdoc />
    public async Task<Result<ProfilingSessionResolution>> GetOrCreateActiveSessionAsync(
        ProfilingSessionCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var validation = ValidateSessionRequest(request);
        if (validation is not null)
        {
            return Failure<ProfilingSessionResolution>(validation);
        }

        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        var active = await context
                            .ProfilingSessions.SingleOrDefaultAsync(
                                x =>
                                    x.LifecycleKey
                                    == EntityFrameworkProfilingStoreConstants.ActiveLifecycleKey,
                                token
                            )
                            .ConfigureAwait(false);
                        if (active is not null)
                        {
                            return Success(
                                new ProfilingSessionResolution(
                                    ProfilingEntityMapper.ToModel(active),
                                    false
                                )
                            );
                        }

                        if (
                            await context
                                .ProfilingInvalidSessions.AnyAsync(
                                    x =>
                                        x.Id == request.Identity.Id
                                        || x.Key == request.Identity.Key,
                                    token
                                )
                                .ConfigureAwait(false)
                        )
                        {
                            return Failure<ProfilingSessionResolution>(
                                new ProfilingInvalidStateError(
                                    "A cleared, deleted, or expired session identity cannot be reused."
                                )
                            );
                        }

                        if (
                            await context
                                .ProfilingSessions.AnyAsync(
                                    x =>
                                        x.Id == request.Identity.Id
                                        || x.Key == request.Identity.Key,
                                    token
                                )
                                .ConfigureAwait(false)
                        )
                        {
                            return Failure<ProfilingSessionResolution>(
                                new ProfilingValidationError(
                                    "The profiling session identity is already in use."
                                )
                            );
                        }

                        var entity = ProfilingEntityMapper.ToEntity(request);
                        context.ProfilingSessions.Add(entity);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        return Success(
                            new ProfilingSessionResolution(
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
    public async Task<Result<ProfilingSession>> GetActiveSessionAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var entity = await context
            .ProfilingSessions.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.LifecycleKey == EntityFrameworkProfilingStoreConstants.ActiveLifecycleKey,
                cancellationToken
            )
            .ConfigureAwait(false);
        return entity is null
            ? Failure<ProfilingSession>(
                new ProfilingInvalidStateError("No profiling session is active.")
            )
            : Success(ProfilingEntityMapper.ToModel(entity));
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingSession>> FindSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsPublicKey(sessionKey))
        {
            return Failure<ProfilingSession>(new ProfilingInvalidKeyError("session"));
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var entity = await context
            .ProfilingSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == sessionKey, cancellationToken)
            .ConfigureAwait(false);
        return entity is null
            ? Failure<ProfilingSession>(
                new NotFoundError($"Profiling session '{sessionKey}' was not found.")
            )
            : Success(ProfilingEntityMapper.ToModel(entity));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ProfilingSession>>> ListSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var entities = await context
            .ProfilingSessions.AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Success<IReadOnlyList<ProfilingSession>>(
            entities
                .OrderByDescending(x => x.StartedUtc)
                .Select(ProfilingEntityMapper.ToModel)
                .ToArray()
        );
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSession>> UpdateSessionMetadataAsync(
        string sessionKey,
        ProfilingSessionMetadata metadata,
        CancellationToken cancellationToken = default
    )
    {
        if (metadata is null)
        {
            return Task.FromResult(
                Failure<ProfilingSession>(
                    new ProfilingValidationError("Session metadata is required.")
                )
            );
        }

        return this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var entity = await context
                    .ProfilingSessions.SingleOrDefaultAsync(x => x.Key == sessionKey, token)
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return Failure<ProfilingSession>(
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
    public async Task<Result<ProfilingSession>> TryTransitionSessionAsync(
        Guid sessionId,
        IReadOnlyCollection<ProfilingSessionState> expectedStates,
        ProfilingSessionState nextState,
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
                            .ProfilingSessions.SingleOrDefaultAsync(x => x.Id == sessionId, token)
                            .ConfigureAwait(false);
                        if (entity is null)
                        {
                            return Failure<ProfilingSession>(
                                new NotFoundError("The profiling session was not found.")
                            );
                        }

                        if (expectedStates?.Contains(entity.State) != true)
                        {
                            return Failure<ProfilingSession>(
                                new ProfilingInvalidStateError(
                                    $"The session cannot transition from '{entity.State}'."
                                )
                            );
                        }

                        if (!IsValidTransition(entity.State, nextState))
                        {
                            return Failure<ProfilingSession>(
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
    public async Task<Result<ProfilingNode>> GetOrCreateNodeAsync(
        ProfilingNodeCorrelation correlation,
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
                            .ProfilingNodes.SingleOrDefaultAsync(
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

                        if (
                            await context
                                .ProfilingNodes.AnyAsync(
                                    x =>
                                        x.Id == proposedNode.Identity.Id
                                        || x.Key == proposedNode.Identity.Key,
                                    token
                                )
                                .ConfigureAwait(false)
                        )
                        {
                            return Failure<ProfilingNode>(
                                new ProfilingValidationError(
                                    "The profiling node identity is already in use."
                                )
                            );
                        }

                        entity = ProfilingEntityMapper.ToEntity(correlation, proposedNode);
                        context.ProfilingNodes.Add(entity);
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
    public Task<Result<ProfilingNodeParticipation>> UpsertParticipationAsync(
        ProfilingNodeParticipation participation,
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
                    return Failure<ProfilingNodeParticipation>(resolved.Error);
                }

                if (
                    participation.SuccessfulCaptureCount < 0
                    || participation.SkippedCaptureCount < 0
                    || participation.FailedCaptureCount < 0
                )
                {
                    return Failure<ProfilingNodeParticipation>(
                        new ProfilingValidationError(
                            "Participation capture totals cannot be negative."
                        )
                    );
                }

                var entity = await context
                    .ProfilingParticipations.SingleOrDefaultAsync(
                        x =>
                            x.SessionId == participation.SessionId
                            && x.NodeId == participation.NodeId,
                        token
                    )
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    entity = ProfilingEntityMapper.ToEntity(participation);
                    context.ProfilingParticipations.Add(entity);
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
                        return Failure<ProfilingNodeParticipation>(
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
    public Task<Result<ProfilingRuntimeContext>> AddRuntimeContextAsync(
        ProfilingRuntimeContext runtimeContext,
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
                    return Failure<ProfilingRuntimeContext>(resolved.Error);
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
                        : Failure<ProfilingRuntimeContext>(
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
    public Task<Result<ProfilingSnapshot>> AddSnapshotAsync(
        ProfilingSnapshot snapshot,
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
                    return Failure<ProfilingSnapshot>(resolved.Error);
                }

                if (
                    snapshot.TimestampUtc < resolved.Session.StartedUtc
                    || snapshot.TimestampUtc > resolved.Session.EndsUtc
                )
                {
                    return Failure<ProfilingSnapshot>(
                        new ProfilingInvalidStateError(
                            "The snapshot timestamp is outside the session collection window."
                        )
                    );
                }

                if (snapshot.Sequence <= 0)
                {
                    return Failure<ProfilingSnapshot>(
                        new ProfilingValidationError("Snapshot sequence must be greater than zero.")
                    );
                }

                var existing = await context
                    .ProfilingSnapshots.AsNoTracking()
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
                        : Failure<ProfilingSnapshot>(
                            new ProfilingInvalidStateError("A stored snapshot cannot be changed.")
                        );
                }

                if (
                    await context
                        .ProfilingSnapshots.AnyAsync(
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
                    return Failure<ProfilingSnapshot>(
                        new ProfilingValidationError(
                            "The snapshot key or node-local sequence is already in use."
                        )
                    );
                }

                context.ProfilingSnapshots.Add(ProfilingEntityMapper.ToEntity(snapshot));
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return Success(snapshot);
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<Result<ProfilingPhaseMarker>> AddPhaseMarkerAsync(
        ProfilingPhaseMarker marker,
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
                                .ProfilingSessions.SingleOrDefaultAsync(
                                    x => x.Id == marker.SessionId && x.Key == marker.SessionKey,
                                    token
                                )
                                .ConfigureAwait(false);
                        if (session is null)
                        {
                            return Failure<ProfilingPhaseMarker>(
                                new NotFoundError("The active profiling session was not found.")
                            );
                        }

                        if (
                            session.State != ProfilingSessionState.Running
                            || marker.TimestampUtc < session.StartedUtc
                            || marker.TimestampUtc > session.EndsUtc
                        )
                        {
                            return Failure<ProfilingPhaseMarker>(
                                new ProfilingInvalidStateError(
                                    "A phase marker requires an active session and a timestamp inside its collection window."
                                )
                            );
                        }

                        if (
                            string.IsNullOrWhiteSpace(marker.Name)
                            || marker.Name.Trim().Length > 100
                        )
                        {
                            return Failure<ProfilingPhaseMarker>(
                                new ProfilingValidationError(
                                    "A phase marker name must contain 1 to 100 characters."
                                )
                            );
                        }

                        var normalized = marker with { Name = marker.Name.Trim() };
                        var existing = session.PhaseMarkers.SingleOrDefault(x => x.Id == marker.Id);
                        if (existing is not null)
                        {
                            var model = ProfilingEntityMapper.ToModel(existing, session.Key);
                            return model == normalized
                                ? Success(model)
                                : Failure<ProfilingPhaseMarker>(
                                    new ProfilingInvalidStateError(
                                        "A stored phase marker cannot be changed."
                                    )
                                );
                        }

                        session.PhaseMarkers.Add(ProfilingEntityMapper.ToEntity(normalized));
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
    public Task<Result<ProfilingActionMarker>> AddActionMarkerAsync(
        ProfilingActionMarker marker,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var resolved = await ResolveSessionNodeAsync(
                        context,
                        marker?.SessionId ?? Guid.Empty,
                        marker?.SessionKey,
                        marker?.NodeId ?? Guid.Empty,
                        marker?.NodeKey,
                        token
                    )
                    .ConfigureAwait(false);
                if (resolved.Error is not null)
                {
                    return Failure<ProfilingActionMarker>(resolved.Error);
                }

                if (
                    resolved.Session.State != ProfilingSessionState.Running
                    || marker.TimestampUtc < resolved.Session.StartedUtc
                    || marker.TimestampUtc > resolved.Session.EndsUtc
                )
                {
                    return Failure<ProfilingActionMarker>(
                        new ProfilingInvalidStateError(
                            "An action marker requires an active session and a timestamp inside its collection window."
                        )
                    );
                }

                if (string.IsNullOrWhiteSpace(marker.Name))
                {
                    return Failure<ProfilingActionMarker>(
                        new ProfilingValidationError("An action marker name is required.")
                    );
                }

                var normalized = marker with { Name = marker.Name.Trim() };
                var existing = resolved.Session.ActionMarkers.SingleOrDefault(x =>
                    x.Id == marker.Id
                );
                if (existing is not null)
                {
                    var model = ProfilingEntityMapper.ToModel(
                        existing,
                        resolved.Session.Key,
                        resolved.Node.Key
                    );
                    return model == normalized
                        ? Success(model)
                        : Failure<ProfilingActionMarker>(
                            new ProfilingInvalidStateError(
                                "A stored action marker cannot be changed."
                            )
                        );
                }

                resolved.Session.ActionMarkers.Add(ProfilingEntityMapper.ToEntity(normalized));
                resolved.Session.AdvanceConcurrencyVersion();
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return Success(normalized);
            },
            cancellationToken,
            transactional: true
        );

    /// <inheritdoc />
    public Task<Result<ProfilingSegment>> UpsertSegmentAsync(
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
                        resolved.Session.State != ProfilingSessionState.Running
                        || segment.StartedUtc < resolved.Session.StartedUtc
                        || segment.StartedUtc > resolved.Session.EndsUtc
                        || segment.Outcome != ProfilingSegmentOutcome.Open
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
                    entity.Outcome != ProfilingSegmentOutcome.Open
                    || normalized.Outcome == ProfilingSegmentOutcome.Open
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
    public Task<Result<ProfilingMetricObservation>> AddMetricObservationAsync(
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
                        .ProfilingSessions.AsNoTracking()
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
                    .ProfilingMetricObservations.AsNoTracking()
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

                context.ProfilingMetricObservations.Add(ProfilingEntityMapper.ToEntity(normalized));
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return Success(normalized);
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<Result<ProfilingSessionData>> GetSessionDataAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsPublicKey(sessionKey))
        {
            return Failure<ProfilingSessionData>(
                new NotFoundError($"Profiling session '{sessionKey}' was not found.")
            );
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var session = await context
            .ProfilingSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == sessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            return Failure<ProfilingSessionData>(
                new NotFoundError($"Profiling session '{sessionKey}' was not found.")
            );
        }

        var participations = await context
            .ProfilingParticipations.AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var snapshots = await context
            .ProfilingSnapshots.AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var observations = await context
            .ProfilingMetricObservations.AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var nodeIds = participations
            .Select(x => x.NodeId)
            .Concat(session.RuntimeContexts.Select(x => x.NodeId))
            .Concat(snapshots.Select(x => x.NodeId))
            .Concat(session.ActionMarkers.Select(x => x.NodeId))
            .Concat(session.Segments.Select(x => x.NodeId))
            .Concat(observations.Select(x => x.NodeId))
            .Distinct()
            .ToArray();
        var nodes = await context
            .ProfilingNodes.AsNoTracking()
            .Where(x => nodeIds.Contains(x.Id))
            .OrderBy(x => x.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var nodeKeys = nodes.ToDictionary(x => x.Id, x => x.Key);

        return Success(
            new ProfilingSessionData
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
                PhaseMarkers = session
                    .PhaseMarkers.OrderBy(x => x.TimestampUtc)
                    .Select(x => ProfilingEntityMapper.ToModel(x, session.Key))
                    .ToArray(),
                ActionMarkers = session
                    .ActionMarkers.OrderBy(x => x.TimestampUtc)
                    .Select(x =>
                        ProfilingEntityMapper.ToModel(
                            x,
                            session.Key,
                            nodeKeys.GetValueOrDefault(x.NodeId)
                        )
                    )
                    .ToArray(),
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
    public async Task<Result<ProfilingSession>> ImportSessionAsync(
        ProfilingSessionData data,
        CancellationToken cancellationToken = default
    )
    {
        var validation = ValidateImportData(data);
        if (validation is not null)
        {
            return Failure<ProfilingSession>(validation);
        }

        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
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
                            await context.ProfilingSessions.AnyAsync(
                                item => item.Id == sessionId || item.Key == sessionKey,
                                token
                            ).ConfigureAwait(false)
                            || await context.ProfilingInvalidSessions.AnyAsync(
                                item => item.Id == sessionId || item.Key == sessionKey,
                                token
                            ).ConfigureAwait(false)
                            || await context.ProfilingNodes.AnyAsync(
                                item =>
                                    nodeIds.Contains(item.Id)
                                    || nodeKeys.Contains(item.Key),
                                token
                            ).ConfigureAwait(false)
                            || await context.ProfilingSnapshots.AnyAsync(
                                item =>
                                    snapshotIds.Contains(item.Id)
                                    || snapshotKeys.Contains(item.Key),
                                token
                            ).ConfigureAwait(false);
                        if (!collision)
                        {
                            foreach (var correlation in correlations)
                            {
                                collision = await context.ProfilingNodes.AnyAsync(
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
                            return Failure<ProfilingSession>(
                                new ProfilingValidationError(
                                    "An imported Profiling identity is already in use."
                                )
                            );
                        }

                        var session = ProfilingEntityMapper.ToEntity(data.Session);
                        session.RuntimeContexts = data.RuntimeContexts
                            .Select(ProfilingEntityMapper.ToEntity)
                            .ToArray();
                        session.PhaseMarkers = data.PhaseMarkers
                            .Select(ProfilingEntityMapper.ToEntity)
                            .ToArray();
                        session.ActionMarkers = data.ActionMarkers
                            .Select(ProfilingEntityMapper.ToEntity)
                            .ToArray();
                        session.Segments = data.Segments
                            .Select(ProfilingEntityMapper.ToEntity)
                            .ToArray();

                        context.ProfilingNodes.AddRange(
                            data.Nodes.Select(item =>
                                ProfilingEntityMapper.ToEntity(item.Correlation, item)
                            )
                        );
                        context.ProfilingSessions.Add(session);
                        context.ProfilingParticipations.AddRange(
                            data.Participations.Select(ProfilingEntityMapper.ToEntity)
                        );
                        context.ProfilingSnapshots.AddRange(
                            data.Snapshots.Select(ProfilingEntityMapper.ToEntity)
                        );
                        context.ProfilingMetricObservations.AddRange(
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
    public async Task<Result<bool>> DeleteSessionAsync(
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
                            .ProfilingSessions.SingleOrDefaultAsync(x => x.Key == sessionKey, token)
                            .ConfigureAwait(false);
                        if (entity is null)
                        {
                            return Failure<bool>(
                                new NotFoundError(
                                    $"Profiling session '{sessionKey}' was not found."
                                )
                            );
                        }

                        if (entity.State == ProfilingSessionState.Running)
                        {
                            return Failure<bool>(
                                new ProfilingInvalidStateError(
                                    "An active profiling session must be stopped before deletion."
                                )
                            );
                        }

                        await AddTombstoneAsync(context, entity, token).ConfigureAwait(false);
                        context.ProfilingSessions.Remove(entity);
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
    public async Task<Result<int>> DeleteUnpinnedSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.DeleteSessionsAsync(
                    context =>
                        context.ProfilingSessions.Where(x =>
                            x.State != ProfilingSessionState.Running && !x.IsPinned
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
    public async Task<Result<ProfilingClearResult>> ClearAsync(
        CancellationToken cancellationToken = default
    )
    {
        await this.lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ExecuteWriteAsync(
                    async (context, token) =>
                    {
                        if (
                            await context
                                .ProfilingSessions.AnyAsync(
                                    x => x.State == ProfilingSessionState.Running,
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
                            .ProfilingSessions.ToListAsync(token)
                            .ConfigureAwait(false);
                        var snapshotCount = await context
                            .ProfilingSnapshots.LongCountAsync(token)
                            .ConfigureAwait(false);
                        foreach (var session in sessions)
                        {
                            await AddTombstoneAsync(context, session, token).ConfigureAwait(false);
                        }

                        context.ProfilingSessions.RemoveRange(sessions);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);

                        var nodes = await context
                            .ProfilingNodes.ToListAsync(token)
                            .ConfigureAwait(false);
                        context.ProfilingNodes.RemoveRange(nodes);
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
    public async Task<Result<int>> ApplyRetentionAsync(
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
                            .ProfilingSessions.Where(x =>
                                x.State != ProfilingSessionState.Running && !x.IsPinned
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

                        context.ProfilingSessions.RemoveRange(candidates);
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

    private async Task<Result<int>> DeleteSessionsAsync(
        Func<TContext, IQueryable<ProfilingSessionEntity>> query,
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

                    context.ProfilingSessions.RemoveRange(sessions);
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                    return Success(sessions.Count);
                },
                cancellationToken,
                transactional: true
            )
            .ConfigureAwait(false);

    private async Task<Result<T>> ExecuteWriteAsync<T>(
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

    private static IResultError ValidateImportData(ProfilingSessionData data)
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
            || data.PhaseMarkers is null
            || data.ActionMarkers is null
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
            || data.PhaseMarkers.Any(item =>
                item is null || item.Id == Guid.Empty || !ValidSession(item.SessionId, item.SessionKey)
            )
            || data.ActionMarkers.Any(item =>
                item is null
                || item.Id == Guid.Empty
                || !ValidSession(item.SessionId, item.SessionKey)
                || !ValidNode(item.NodeId, item.NodeKey)
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
        ProfilingSessionEntity session,
        CancellationToken cancellationToken
    )
    {
        if (
            !await context
                .ProfilingInvalidSessions.AnyAsync(
                    x => x.Id == session.Id || x.Key == session.Key,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            context.ProfilingInvalidSessions.Add(
                new ProfilingInvalidSessionEntity { Id = session.Id, Key = session.Key }
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
            .ProfilingSessions.SingleOrDefaultAsync(
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
            .ProfilingNodes.SingleOrDefaultAsync(
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
            .ProfilingSessions.AsNoTracking()
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
            .ProfilingNodes.AsNoTracking()
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

    private static IResultError ValidateSessionRequest(ProfilingSessionCreateRequest request)
    {
        if (request is null)
        {
            return new ProfilingValidationError("A profiling session request is required.");
        }

        if (
            request.Identity.Id == Guid.Empty
            || !IsPublicKey(request.Identity.Key)
            || request.SamplingInterval < ProfilingOptions.MinimumSamplingInterval
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
        ProfilingNodeCorrelation correlation,
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
        ProfilingSessionState current,
        ProfilingSessionState next
    ) => current == next || current == ProfilingSessionState.Running && IsTerminal(next);

    private static bool IsTerminal(ProfilingSessionState state) =>
        state
            is ProfilingSessionState.Completed
                or ProfilingSessionState.CompletedWithWarnings
                or ProfilingSessionState.Stopped
                or ProfilingSessionState.Failed;

    private static bool IsTerminal(ProfilingParticipationState state) =>
        state
            is ProfilingParticipationState.Completed
                or ProfilingParticipationState.Stopped
                or ProfilingParticipationState.Failed;

    private static int ParticipationRank(ProfilingParticipationState state) =>
        state switch
        {
            ProfilingParticipationState.Accepted => 0,
            ProfilingParticipationState.Collecting => 1,
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
        ProfilingSessionEntity Session,
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
