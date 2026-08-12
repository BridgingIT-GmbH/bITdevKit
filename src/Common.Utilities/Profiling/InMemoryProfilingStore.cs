// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Stores profiling diagnostic sessions in process-local memory.
/// </summary>
/// <remarks>
/// One process-local lock serializes lifecycle checks and mutations. The provider is ephemeral
/// and deliberately reports that it cannot coordinate independent application processes.
/// </remarks>
/// <example><code>IProfilingStore store = new InMemoryProfilingStore();</code></example>
public sealed class InMemoryProfilingStore : IProfilingStore
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, ProfilingSession> sessions = [];
    private readonly Dictionary<string, Guid> sessionKeys = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> invalidSessionIds = [];
    private readonly HashSet<string> invalidSessionKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ProfilingNode> nodes = [];
    private readonly Dictionary<NodeCorrelationKey, Guid> nodeCorrelations = [];
    private readonly Dictionary<
        (Guid SessionId, Guid NodeId),
        ProfilingNodeParticipation
    > participations = [];
    private readonly Dictionary<
        (Guid SessionId, Guid NodeId),
        ProfilingRuntimeContext
    > runtimeContexts = [];
    private readonly Dictionary<Guid, ProfilingSnapshot> snapshots = [];
    private readonly Dictionary<string, Guid> snapshotKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ProfilingPhaseMarker> phaseMarkers = [];
    private readonly Dictionary<Guid, ProfilingActionMarker> actionMarkers = [];
    private readonly Dictionary<Guid, ProfilingSegment> segments = [];
    private readonly Dictionary<Guid, ProfilingMetricObservation> metricObservations = [];

    /// <inheritdoc />
    public ProfilingStoreCapabilities Capabilities { get; } = new(false);

    /// <inheritdoc />
    public Task<Result<ProfilingSessionResolution>> GetOrCreateActiveSessionAsync(
        ProfilingSessionCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var active = this.sessions.Values.SingleOrDefault(IsActive);
            if (active is not null)
            {
                return Success(new ProfilingSessionResolution(Clone(active), false));
            }

            var validation = ValidateSessionRequest(request);
            if (validation is not null)
            {
                return Failure<ProfilingSessionResolution>(validation);
            }

            if (
                this.invalidSessionIds.Contains(request.Identity.Id)
                || this.invalidSessionKeys.Contains(request.Identity.Key)
            )
            {
                return Failure<ProfilingSessionResolution>(
                    new ProfilingInvalidStateError(
                        "A cleared, deleted, or expired session identity cannot be reused."
                    )
                );
            }

            if (
                this.sessions.ContainsKey(request.Identity.Id)
                || this.sessionKeys.ContainsKey(request.Identity.Key)
            )
            {
                return Failure<ProfilingSessionResolution>(
                    new ProfilingValidationError(
                        "The profiling session identity is already in use."
                    )
                );
            }

            var session = new ProfilingSession
            {
                Identity = request.Identity,
                Name = NormalizeOptional(request.Name),
                State = ProfilingSessionState.Running,
                StartedUtc = request.StartedUtc,
                EndsUtc = request.StartedUtc.Add(request.Duration),
                SamplingInterval = request.SamplingInterval,
                Duration = request.Duration,
                Tags = CloneStrings(request.Tags),
            };

            this.sessions.Add(session.Identity.Id, session);
            this.sessionKeys.Add(session.Identity.Key, session.Identity.Id);

            return Success(new ProfilingSessionResolution(Clone(session), true));
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSession>> GetActiveSessionAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var session = this.sessions.Values.SingleOrDefault(IsActive);
            return session is null
                ? Failure<ProfilingSession>(
                    new ProfilingInvalidStateError("No profiling session is active.")
                )
                : Success(Clone(session));
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSession>> FindSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!IsPublicKey(sessionKey))
            {
                return Failure<ProfilingSession>(new ProfilingInvalidKeyError("session"));
            }

            return this.TryGetSession(sessionKey, out var session)
                ? Success(Clone(session))
                : Failure<ProfilingSession>(
                    new NotFoundError($"Profiling session '{sessionKey}' was not found.")
                );
        }
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ProfilingSession>>> ListSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            return Success<IReadOnlyList<ProfilingSession>>(
                this.sessions.Values.OrderByDescending(x => x.StartedUtc).Select(Clone).ToArray()
            );
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSession>> UpdateSessionMetadataAsync(
        string sessionKey,
        ProfilingSessionMetadata metadata,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (metadata is null)
            {
                return Failure<ProfilingSession>(
                    new ProfilingValidationError("Session metadata is required.")
                );
            }

            if (!this.TryGetSession(sessionKey, out var session))
            {
                return Failure<ProfilingSession>(
                    new NotFoundError($"Profiling session '{sessionKey}' was not found.")
                );
            }

            var updated = session with
            {
                Name = NormalizeOptional(metadata.Name),
                Tags = CloneStrings(metadata.Tags),
                Note = NormalizeOptional(metadata.Note),
                IsPinned = metadata.IsPinned,
            };
            this.sessions[session.Identity.Id] = updated;

            return Success(Clone(updated));
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSession>> TryTransitionSessionAsync(
        Guid sessionId,
        IReadOnlyCollection<ProfilingSessionState> expectedStates,
        ProfilingSessionState nextState,
        DateTimeOffset transitionedUtc,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.sessions.TryGetValue(sessionId, out var session))
            {
                return Failure<ProfilingSession>(
                    new NotFoundError("The profiling session was not found.")
                );
            }

            if (expectedStates?.Contains(session.State) != true)
            {
                return Failure<ProfilingSession>(
                    new ProfilingInvalidStateError(
                        $"The session cannot transition from '{session.State}'."
                    )
                );
            }

            if (!IsValidTransition(session.State, nextState))
            {
                return Failure<ProfilingSession>(
                    new ProfilingInvalidStateError(
                        $"The session cannot transition from '{session.State}' to '{nextState}'."
                    )
                );
            }

            if (session.State == nextState)
            {
                return Success(Clone(session));
            }

            var updated = session with
            {
                State = nextState,
                CompletedUtc = IsTerminal(nextState) ? transitionedUtc : session.CompletedUtc,
            };
            this.sessions[sessionId] = updated;

            return Success(Clone(updated));
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingNode>> GetOrCreateNodeAsync(
        ProfilingNodeCorrelation correlation,
        ProfilingNode proposedNode,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var validation = ValidateNode(correlation, proposedNode);
            if (validation is not null)
            {
                return Failure<ProfilingNode>(validation);
            }

            var correlationKey = NodeCorrelationKey.Create(correlation);
            if (
                this.nodeCorrelations.TryGetValue(correlationKey, out var existingId)
                && this.nodes.TryGetValue(existingId, out var existing)
            )
            {
                return Success(Clone(existing));
            }

            if (
                this.nodes.ContainsKey(proposedNode.Identity.Id)
                || this.nodes.Values.Any(x =>
                    string.Equals(
                        x.Identity.Key,
                        proposedNode.Identity.Key,
                        StringComparison.Ordinal
                    )
                )
            )
            {
                return Failure<ProfilingNode>(
                    new ProfilingValidationError("The profiling node identity is already in use.")
                );
            }

            var stored = Clone(proposedNode with { Correlation = correlation });
            this.nodes.Add(stored.Identity.Id, stored);
            this.nodeCorrelations.Add(correlationKey, stored.Identity.Id);
            return Success(Clone(stored));
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingNodeParticipation>> UpsertParticipationAsync(
        ProfilingNodeParticipation participation,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var validation = this.ValidateSessionNodeRecord(
                participation?.SessionId ?? Guid.Empty,
                participation?.SessionKey,
                participation?.NodeId ?? Guid.Empty,
                participation?.NodeKey
            );
            if (validation is not null)
            {
                return Failure<ProfilingNodeParticipation>(validation);
            }

            if (
                participation.SuccessfulCaptureCount < 0
                || participation.SkippedCaptureCount < 0
                || participation.FailedCaptureCount < 0
            )
            {
                return Failure<ProfilingNodeParticipation>(
                    new ProfilingValidationError("Participation capture totals cannot be negative.")
                );
            }

            var key = (participation.SessionId, participation.NodeId);
            if (this.participations.TryGetValue(key, out var existing))
            {
                if (
                    existing.Role != participation.Role
                    || participation.SuccessfulCaptureCount < existing.SuccessfulCaptureCount
                    || participation.SkippedCaptureCount < existing.SkippedCaptureCount
                    || participation.FailedCaptureCount < existing.FailedCaptureCount
                    || IsTerminal(existing.State) && existing.State != participation.State
                    || ParticipationRank(participation.State) < ParticipationRank(existing.State)
                )
                {
                    return Failure<ProfilingNodeParticipation>(
                        new ProfilingInvalidStateError(
                            "Node participation role, state, and capture totals cannot move backwards."
                        )
                    );
                }
            }

            var stored = Clone(participation);
            this.participations[key] = stored;
            return Success(Clone(stored));
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingRuntimeContext>> AddRuntimeContextAsync(
        ProfilingRuntimeContext context,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var validation = this.ValidateSessionNodeRecord(
                context?.SessionId ?? Guid.Empty,
                context?.SessionKey,
                context?.NodeId ?? Guid.Empty,
                context?.NodeKey
            );
            if (validation is not null)
            {
                return Failure<ProfilingRuntimeContext>(validation);
            }

            var key = (context.SessionId, context.NodeId);
            if (this.runtimeContexts.TryGetValue(key, out var existing))
            {
                return existing == context
                    ? Success(Clone(existing))
                    : Failure<ProfilingRuntimeContext>(
                        new ProfilingInvalidStateError(
                            "Runtime context is immutable once stored for a session node."
                        )
                    );
            }

            var stored = Clone(context);
            this.runtimeContexts.Add(key, stored);
            return Success(Clone(stored));
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSnapshot>> AddSnapshotAsync(
        ProfilingSnapshot snapshot,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var validation = this.ValidateSessionNodeRecord(
                snapshot?.SessionId ?? Guid.Empty,
                snapshot?.SessionKey,
                snapshot?.NodeId ?? Guid.Empty,
                snapshot?.NodeKey
            );
            if (validation is not null)
            {
                return Failure<ProfilingSnapshot>(validation);
            }

            if (!this.IsInsideCollectionWindow(snapshot.SessionId, snapshot.TimestampUtc))
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

            if (this.snapshots.TryGetValue(snapshot.Identity.Id, out var existing))
            {
                return existing == snapshot
                    ? Success(existing)
                    : Failure<ProfilingSnapshot>(
                        new ProfilingInvalidStateError("A stored snapshot cannot be changed.")
                    );
            }

            if (
                this.snapshotKeys.ContainsKey(snapshot.Identity.Key)
                || this.snapshots.Values.Any(x =>
                    x.SessionId == snapshot.SessionId
                    && x.NodeId == snapshot.NodeId
                    && x.Sequence == snapshot.Sequence
                )
            )
            {
                return Failure<ProfilingSnapshot>(
                    new ProfilingValidationError(
                        "The snapshot key or node-local sequence is already in use."
                    )
                );
            }

            this.snapshots.Add(snapshot.Identity.Id, snapshot);
            this.snapshotKeys.Add(snapshot.Identity.Key, snapshot.Identity.Id);
            return Success(snapshot);
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingPhaseMarker>> AddPhaseMarkerAsync(
        ProfilingPhaseMarker marker,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (
                marker is null
                || !this.sessions.TryGetValue(marker.SessionId, out var session)
                || !string.Equals(session.Identity.Key, marker.SessionKey, StringComparison.Ordinal)
            )
            {
                return Failure<ProfilingPhaseMarker>(
                    new NotFoundError("The active profiling session was not found.")
                );
            }

            if (
                !IsActive(session)
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

            if (string.IsNullOrWhiteSpace(marker.Name) || marker.Name.Trim().Length > 100)
            {
                return Failure<ProfilingPhaseMarker>(
                    new ProfilingValidationError(
                        "A phase marker name must contain 1 to 100 characters."
                    )
                );
            }

            var normalized = marker with { Name = marker.Name.Trim() };
            return this.AddImmutable(this.phaseMarkers, normalized.Id, normalized, "phase marker");
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingActionMarker>> AddActionMarkerAsync(
        ProfilingActionMarker marker,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var validation = this.ValidateSessionNodeRecord(
                marker?.SessionId ?? Guid.Empty,
                marker?.SessionKey,
                marker?.NodeId ?? Guid.Empty,
                marker?.NodeKey
            );
            if (validation is not null)
            {
                return Failure<ProfilingActionMarker>(validation);
            }

            if (
                !this.sessions.TryGetValue(marker.SessionId, out var session)
                || !IsActive(session)
                || marker.TimestampUtc < session.StartedUtc
                || marker.TimestampUtc > session.EndsUtc
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
            return this.AddImmutable(
                this.actionMarkers,
                normalized.Id,
                normalized,
                "action marker"
            );
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSegment>> UpsertSegmentAsync(
        ProfilingSegment segment,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var validation = this.ValidateSessionNodeRecord(
                segment?.SessionId ?? Guid.Empty,
                segment?.SessionKey,
                segment?.NodeId ?? Guid.Empty,
                segment?.NodeKey
            );
            if (validation is not null)
            {
                return Failure<ProfilingSegment>(validation);
            }

            if (segment.Id == Guid.Empty || string.IsNullOrWhiteSpace(segment.Name))
            {
                return Failure<ProfilingSegment>(
                    new ProfilingValidationError("A segment identity and name are required.")
                );
            }

            if (segment.ParentSegmentId is { } parentId)
            {
                if (
                    !this.segments.TryGetValue(parentId, out var parent)
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

            var normalized = Clone(segment with { Name = segment.Name.Trim() });
            if (!this.segments.TryGetValue(segment.Id, out var existing))
            {
                if (
                    !this.sessions.TryGetValue(segment.SessionId, out var session)
                    || !IsActive(session)
                    || segment.StartedUtc < session.StartedUtc
                    || segment.StartedUtc > session.EndsUtc
                    || segment.Outcome != ProfilingSegmentOutcome.Open
                )
                {
                    return Failure<ProfilingSegment>(
                        new ProfilingInvalidStateError(
                            "A new segment must open inside an active session collection window."
                        )
                    );
                }

                this.segments.Add(segment.Id, normalized);
                return Success(Clone(normalized));
            }

            if (existing == normalized)
            {
                return Success(Clone(existing));
            }

            if (
                existing.Outcome != ProfilingSegmentOutcome.Open
                || normalized.Outcome == ProfilingSegmentOutcome.Open
                || existing.SessionId != normalized.SessionId
                || existing.NodeId != normalized.NodeId
                || existing.StartedUtc != normalized.StartedUtc
                || !string.Equals(existing.Name, normalized.Name, StringComparison.Ordinal)
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

            this.segments[segment.Id] = normalized;
            return Success(Clone(normalized));
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingMetricObservation>> AddMetricObservationAsync(
        ProfilingMetricObservation observation,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var validation = this.ValidateSessionNodeRecord(
                observation?.SessionId ?? Guid.Empty,
                observation?.SessionKey,
                observation?.NodeId ?? Guid.Empty,
                observation?.NodeKey
            );
            if (validation is not null)
            {
                return Failure<ProfilingMetricObservation>(validation);
            }

            if (
                observation.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(observation.MetricIdentifier)
                || !this.IsInsideCollectionWindow(observation.SessionId, observation.TimestampUtc)
            )
            {
                return Failure<ProfilingMetricObservation>(
                    new ProfilingValidationError(
                        "A metric identity, stable identifier, and timestamp inside the collection window are required."
                    )
                );
            }

            if (
                observation.SegmentId is { } segmentId
                && (
                    !this.segments.TryGetValue(segmentId, out var segment)
                    || segment.SessionId != observation.SessionId
                    || segment.NodeId != observation.NodeId
                )
            )
            {
                return Failure<ProfilingMetricObservation>(
                    new ProfilingValidationError(
                        "An ambient metric segment must belong to the same session and node."
                    )
                );
            }

            var normalized = observation with
            {
                MetricIdentifier = observation.MetricIdentifier.Trim(),
                Unit = NormalizeOptional(observation.Unit),
            };
            return this.AddImmutable(
                this.metricObservations,
                normalized.Id,
                normalized,
                "metric observation"
            );
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSessionData>> GetSessionDataAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.TryGetSession(sessionKey, out var session))
            {
                return Failure<ProfilingSessionData>(
                    new NotFoundError($"Profiling session '{sessionKey}' was not found.")
                );
            }

            var sessionId = session.Identity.Id;
            var sessionParticipations = this
                .participations.Values.Where(x => x.SessionId == sessionId)
                .OrderBy(x => x.NodeKey, StringComparer.Ordinal)
                .Select(Clone)
                .ToArray();
            var nodeIds = sessionParticipations.Select(x => x.NodeId).ToHashSet();

            return Success(
                new ProfilingSessionData
                {
                    Session = Clone(session),
                    Participations = sessionParticipations,
                    Nodes = this
                        .nodes.Values.Where(x => nodeIds.Contains(x.Identity.Id))
                        .OrderBy(x => x.Identity.Key, StringComparer.Ordinal)
                        .Select(Clone)
                        .ToArray(),
                    RuntimeContexts = this
                        .runtimeContexts.Values.Where(x => x.SessionId == sessionId)
                        .OrderBy(x => x.NodeKey, StringComparer.Ordinal)
                        .Select(Clone)
                        .ToArray(),
                    Snapshots = this
                        .snapshots.Values.Where(x => x.SessionId == sessionId)
                        .OrderBy(x => x.TimestampUtc)
                        .ThenBy(x => x.NodeKey, StringComparer.Ordinal)
                        .ThenBy(x => x.Sequence)
                        .ToArray(),
                    PhaseMarkers = this
                        .phaseMarkers.Values.Where(x => x.SessionId == sessionId)
                        .OrderBy(x => x.TimestampUtc)
                        .ToArray(),
                    ActionMarkers = this
                        .actionMarkers.Values.Where(x => x.SessionId == sessionId)
                        .OrderBy(x => x.TimestampUtc)
                        .ToArray(),
                    Segments = this
                        .segments.Values.Where(x => x.SessionId == sessionId)
                        .OrderBy(x => x.StartedUtc)
                        .Select(Clone)
                        .ToArray(),
                    MetricObservations = this
                        .metricObservations.Values.Where(x => x.SessionId == sessionId)
                        .OrderBy(x => x.TimestampUtc)
                        .ToArray(),
                }
            );
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSession>> ImportSessionAsync(
        ProfilingSessionData data,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var validation = this.ValidateImportData(data);
            if (validation is not null)
            {
                return Failure<ProfilingSession>(validation);
            }

            var session = Clone(data.Session);
            this.sessions.Add(session.Identity.Id, session);
            this.sessionKeys.Add(session.Identity.Key, session.Identity.Id);

            foreach (var node in data.Nodes)
            {
                var stored = Clone(node);
                this.nodes.Add(stored.Identity.Id, stored);
                this.nodeCorrelations.Add(
                    NodeCorrelationKey.Create(stored.Correlation),
                    stored.Identity.Id
                );
            }

            foreach (var participation in data.Participations)
            {
                this.participations.Add(
                    (participation.SessionId, participation.NodeId),
                    Clone(participation)
                );
            }

            foreach (var context in data.RuntimeContexts)
            {
                this.runtimeContexts.Add((context.SessionId, context.NodeId), Clone(context));
            }

            foreach (var snapshot in data.Snapshots)
            {
                this.snapshots.Add(snapshot.Identity.Id, snapshot);
                this.snapshotKeys.Add(snapshot.Identity.Key, snapshot.Identity.Id);
            }

            foreach (var marker in data.PhaseMarkers)
            {
                this.phaseMarkers.Add(marker.Id, marker);
            }

            foreach (var marker in data.ActionMarkers)
            {
                this.actionMarkers.Add(marker.Id, marker);
            }

            foreach (var segment in data.Segments)
            {
                this.segments.Add(segment.Id, Clone(segment));
            }

            foreach (var observation in data.MetricObservations)
            {
                this.metricObservations.Add(observation.Id, observation);
            }

            return Success(Clone(session));
        }
    }

    /// <inheritdoc />
    public Task<Result<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.TryGetSession(sessionKey, out var session))
            {
                return Failure<bool>(
                    new NotFoundError($"Profiling session '{sessionKey}' was not found.")
                );
            }

            if (IsActive(session))
            {
                return Failure<bool>(
                    new ProfilingInvalidStateError(
                        "An active profiling session must be stopped before deletion."
                    )
                );
            }

            this.DeleteSession(session);
            return Success(true);
        }
    }

    /// <inheritdoc />
    public Task<Result<int>> DeleteUnpinnedSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var candidates = this
                .sessions.Values.Where(x => IsTerminal(x.State) && !x.IsPinned)
                .ToArray();
            foreach (var session in candidates)
            {
                this.DeleteSession(session);
            }

            return Success(candidates.Length);
        }
    }

    /// <inheritdoc />
    public Task<Result<ProfilingClearResult>> ClearAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (this.sessions.Values.Any(IsActive))
            {
                return Failure<ProfilingClearResult>(
                    new ProfilingInvalidStateError(
                        "The active profiling session must be stopped before clearing the store."
                    )
                );
            }

            var result = new ProfilingClearResult(this.sessions.Count, this.snapshots.Count);
            foreach (var session in this.sessions.Values)
            {
                this.invalidSessionIds.Add(session.Identity.Id);
                this.invalidSessionKeys.Add(session.Identity.Key);
            }

            this.sessions.Clear();
            this.sessionKeys.Clear();
            this.nodes.Clear();
            this.nodeCorrelations.Clear();
            this.participations.Clear();
            this.runtimeContexts.Clear();
            this.snapshots.Clear();
            this.snapshotKeys.Clear();
            this.phaseMarkers.Clear();
            this.actionMarkers.Clear();
            this.segments.Clear();
            this.metricObservations.Clear();

            return Success(result);
        }
    }

    /// <inheritdoc />
    public Task<Result<int>> ApplyRetentionAsync(
        int maximumRetainedSessions,
        TimeSpan maximumSessionAge,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (maximumRetainedSessions <= 0 || maximumSessionAge <= TimeSpan.Zero)
            {
                return Failure<int>(
                    new ProfilingValidationError(
                        "Retention requires a positive session count and maximum age."
                    )
                );
            }

            var terminal = this
                .sessions.Values.Where(x => IsTerminal(x.State) && !x.IsPinned)
                .OrderByDescending(TerminalTimestamp)
                .ToArray();
            var ageThreshold = utcNow.Subtract(maximumSessionAge);
            var candidates = terminal
                .Where(
                    (session, index) =>
                        TerminalTimestamp(session) < ageThreshold
                        || index >= maximumRetainedSessions
                )
                .DistinctBy(x => x.Identity.Id)
                .ToArray();

            foreach (var session in candidates)
            {
                this.DeleteSession(session);
            }

            return Success(candidates.Length);
        }
    }

    private static Task<Result<T>> Success<T>(T value) => Task.FromResult(Result<T>.Success(value));

    private static Task<Result<T>> Failure<T>(IResultError error) =>
        Task.FromResult(Result<T>.Failure().WithError(error));

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

        if (proposedNode.Correlation is not null && proposedNode.Correlation != correlation)
        {
            return new ProfilingValidationError(
                "The proposed node correlation does not match the requested Broadcast registration."
            );
        }

        return null;
    }

    private IResultError ValidateSessionNodeRecord(
        Guid sessionId,
        string sessionKey,
        Guid nodeId,
        string nodeKey
    )
    {
        if (
            !this.sessions.TryGetValue(sessionId, out var session)
            || !string.Equals(session.Identity.Key, sessionKey, StringComparison.Ordinal)
        )
        {
            return new NotFoundError("The profiling session was not found.");
        }

        if (
            !this.nodes.TryGetValue(nodeId, out var node)
            || !string.Equals(node.Identity.Key, nodeKey, StringComparison.Ordinal)
        )
        {
            return new NotFoundError("The profiling node was not found.");
        }

        return null;
    }

    private IResultError ValidateImportData(ProfilingSessionData data)
    {
        var session = data?.Session;
        if (
            session is null
            || session.Identity.Id == Guid.Empty
            || !IsPublicKey(session.Identity.Key)
            || !IsTerminal(session.State)
            || this.sessions.ContainsKey(session.Identity.Id)
            || this.sessionKeys.ContainsKey(session.Identity.Key)
            || this.invalidSessionIds.Contains(session.Identity.Id)
            || this.invalidSessionKeys.Contains(session.Identity.Key)
        )
        {
            return new ProfilingValidationError(
                "An imported Profiling session must have a fresh terminal identity."
            );
        }

        var nodes = data.Nodes.SafeNull().ToArray();
        if (
            nodes.Any(node => node is null)
            || nodes.Any(node =>
                node.Identity.Id == Guid.Empty
                || !IsPublicKey(node.Identity.Key)
                || node.Correlation is null
                || this.nodes.ContainsKey(node.Identity.Id)
                || this.nodes.Values.Any(existing => existing.Identity.Key == node.Identity.Key)
                || this.nodeCorrelations.ContainsKey(NodeCorrelationKey.Create(node.Correlation))
            )
            || nodes.Select(node => node.Identity.Id).Distinct().Count() != nodes.Length
            || nodes.Select(node => node.Identity.Key).Distinct(StringComparer.Ordinal).Count()
                != nodes.Length
            || nodes.Select(node => NodeCorrelationKey.Create(node.Correlation)).Distinct().Count()
                != nodes.Length
        )
        {
            return new ProfilingValidationError(
                "Imported Profiling nodes must have fresh unique identities and correlations."
            );
        }

        var nodeIds = nodes.Select(node => node.Identity.Id).ToHashSet();
        var nodeKeys = nodes.ToDictionary(node => node.Identity.Id, node => node.Identity.Key);
        bool HasValidNode(Guid id, string key) =>
            nodeIds.Contains(id)
            && nodeKeys.TryGetValue(id, out var expected)
            && string.Equals(expected, key, StringComparison.Ordinal);
        bool HasValidSession(Guid id, string key) =>
            id == session.Identity.Id
            && string.Equals(key, session.Identity.Key, StringComparison.Ordinal);

        if (
            data.Participations.SafeNull().Any(item =>
                item is null
                || !HasValidSession(item.SessionId, item.SessionKey)
                || !HasValidNode(item.NodeId, item.NodeKey)
            )
            || data.RuntimeContexts.SafeNull().Any(item =>
                item is null
                || !HasValidSession(item.SessionId, item.SessionKey)
                || !HasValidNode(item.NodeId, item.NodeKey)
            )
            || data.Snapshots.SafeNull().Any(item =>
                item is null
                || item.Identity.Id == Guid.Empty
                || !IsPublicKey(item.Identity.Key)
                || !HasValidSession(item.SessionId, item.SessionKey)
                || !HasValidNode(item.NodeId, item.NodeKey)
                || this.snapshots.ContainsKey(item.Identity.Id)
                || this.snapshotKeys.ContainsKey(item.Identity.Key)
            )
            || data.PhaseMarkers.SafeNull().Any(item =>
                item is null
                || item.Id == Guid.Empty
                || this.phaseMarkers.ContainsKey(item.Id)
                || !HasValidSession(item.SessionId, item.SessionKey)
            )
            || data.ActionMarkers.SafeNull().Any(item =>
                item is null
                || item.Id == Guid.Empty
                || this.actionMarkers.ContainsKey(item.Id)
                || !HasValidSession(item.SessionId, item.SessionKey)
                || !HasValidNode(item.NodeId, item.NodeKey)
            )
            || data.Segments.SafeNull().Any(item =>
                item is null
                || item.Id == Guid.Empty
                || this.segments.ContainsKey(item.Id)
                || !HasValidSession(item.SessionId, item.SessionKey)
                || !HasValidNode(item.NodeId, item.NodeKey)
            )
            || data.MetricObservations.SafeNull().Any(item =>
                item is null
                || item.Id == Guid.Empty
                || this.metricObservations.ContainsKey(item.Id)
                || !HasValidSession(item.SessionId, item.SessionKey)
                || !HasValidNode(item.NodeId, item.NodeKey)
            )
        )
        {
            return new ProfilingValidationError(
                "The imported Profiling graph contains invalid identities or relationships."
            );
        }

        var snapshots = data.Snapshots.SafeNull().ToArray();
        var segments = data.Segments.SafeNull().ToArray();
        var segmentNodes = segments.ToDictionary(item => item.Id, item => item.NodeId);
        if (
            snapshots.Select(item => item.Identity.Id).Distinct().Count() != snapshots.Length
            || snapshots.Select(item => item.Identity.Key).Distinct(StringComparer.Ordinal).Count()
                != snapshots.Length
            || data.Participations.SafeNull().Select(item => (item.SessionId, item.NodeId)).Distinct().Count()
                != data.Participations.SafeNull().Count()
            || data.RuntimeContexts.SafeNull().Select(item => (item.SessionId, item.NodeId)).Distinct().Count()
                != data.RuntimeContexts.SafeNull().Count()
            || segments.Select(item => item.Id).Distinct().Count() != segments.Length
            || data.PhaseMarkers.SafeNull().Select(item => item.Id).Distinct().Count()
                != data.PhaseMarkers.SafeNull().Count()
            || data.ActionMarkers.SafeNull().Select(item => item.Id).Distinct().Count()
                != data.ActionMarkers.SafeNull().Count()
            || data.MetricObservations.SafeNull().Select(item => item.Id).Distinct().Count()
                != data.MetricObservations.SafeNull().Count()
            || segments.Any(item =>
                item.ParentSegmentId is { } parent
                && (!segmentNodes.TryGetValue(parent, out var parentNode) || parentNode != item.NodeId)
            )
            || data.MetricObservations.SafeNull().Any(item =>
                item.SegmentId is { } segment
                && (!segmentNodes.TryGetValue(segment, out var segmentNode) || segmentNode != item.NodeId)
            )
        )
        {
            return new ProfilingValidationError(
                "The imported Profiling graph contains duplicate or inconsistent relationships."
            );
        }

        return null;
    }

    private bool IsInsideCollectionWindow(Guid sessionId, DateTimeOffset timestampUtc) =>
        this.sessions.TryGetValue(sessionId, out var session)
        && timestampUtc >= session.StartedUtc
        && timestampUtc <= session.EndsUtc;

    private bool TryGetSession(string sessionKey, out ProfilingSession session)
    {
        session = null;
        return IsPublicKey(sessionKey)
            && this.sessionKeys.TryGetValue(sessionKey, out var sessionId)
            && this.sessions.TryGetValue(sessionId, out session);
    }

    private Task<Result<T>> AddImmutable<T>(
        Dictionary<Guid, T> records,
        Guid id,
        T value,
        string recordName
    )
    {
        if (id == Guid.Empty)
        {
            return Failure<T>(
                new ProfilingValidationError($"A {recordName} identity is required.")
            );
        }

        if (records.TryGetValue(id, out var existing))
        {
            return EqualityComparer<T>.Default.Equals(existing, value)
                ? Success(existing)
                : Failure<T>(
                    new ProfilingInvalidStateError($"A stored {recordName} cannot be changed.")
                );
        }

        records.Add(id, value);
        return Success(value);
    }

    private void DeleteSession(ProfilingSession session)
    {
        var sessionId = session.Identity.Id;
        this.invalidSessionIds.Add(sessionId);
        this.invalidSessionKeys.Add(session.Identity.Key);
        this.sessions.Remove(sessionId);
        this.sessionKeys.Remove(session.Identity.Key);

        RemoveKeysWhere(this.participations, key => key.SessionId == sessionId);
        RemoveKeysWhere(this.runtimeContexts, key => key.SessionId == sessionId);
        RemoveValuesWhere(this.phaseMarkers, value => value.SessionId == sessionId);
        RemoveValuesWhere(this.actionMarkers, value => value.SessionId == sessionId);
        RemoveValuesWhere(this.segments, value => value.SessionId == sessionId);
        RemoveValuesWhere(this.metricObservations, value => value.SessionId == sessionId);

        foreach (
            var snapshot in this.snapshots.Values.Where(x => x.SessionId == sessionId).ToArray()
        )
        {
            this.snapshots.Remove(snapshot.Identity.Id);
            this.snapshotKeys.Remove(snapshot.Identity.Key);
        }
    }

    private static void RemoveKeysWhere<TKey, TValue>(
        Dictionary<TKey, TValue> source,
        Func<TKey, bool> predicate
    )
        where TKey : notnull
    {
        foreach (var key in source.Keys.Where(predicate).ToArray())
        {
            source.Remove(key);
        }
    }

    private static void RemoveValuesWhere<TKey, TValue>(
        Dictionary<TKey, TValue> source,
        Func<TValue, bool> predicate
    )
        where TKey : notnull
    {
        foreach (var pair in source.Where(pair => predicate(pair.Value)).ToArray())
        {
            source.Remove(pair.Key);
        }
    }

    private static bool IsActive(ProfilingSession session) =>
        session.State == ProfilingSessionState.Running;

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

    private static bool IsValidTransition(
        ProfilingSessionState current,
        ProfilingSessionState next
    ) => current == next || current == ProfilingSessionState.Running && IsTerminal(next);

    private static int ParticipationRank(ProfilingParticipationState state) =>
        state switch
        {
            ProfilingParticipationState.Accepted => 0,
            ProfilingParticipationState.Collecting => 1,
            _ => 2,
        };

    private static DateTimeOffset TerminalTimestamp(ProfilingSession session) =>
        session.CompletedUtc ?? session.EndsUtc;

    private static bool IsPublicKey(string value) =>
        value?.Length == 8
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9');

    private static string NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> CloneStrings(IEnumerable<string> values) =>
        values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() ?? [];

    private static ProfilingSession Clone(ProfilingSession value) =>
        value with
        {
            Tags = CloneStrings(value.Tags),
        };

    private static ProfilingNode Clone(ProfilingNode value) =>
        value with
        {
            Correlation = value.Correlation is null ? null : value.Correlation with { },
        };

    private static ProfilingNodeParticipation Clone(ProfilingNodeParticipation value) =>
        value with
        { };

    private static ProfilingRuntimeContext Clone(ProfilingRuntimeContext value) => value with { };

    private static ProfilingSegment Clone(ProfilingSegment value) =>
        value with
        {
            Tags = CloneStrings(value.Tags),
        };

    private readonly record struct NodeCorrelationKey(
        string BroadcastNodeIdentity,
        long ProcessStartedUtcTicks
    )
    {
        public static NodeCorrelationKey Create(ProfilingNodeCorrelation correlation) =>
            new(correlation.BroadcastNodeIdentity.Trim(), correlation.ProcessStartedUtc.UtcTicks);
    }
}
