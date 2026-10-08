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
/// <example><code>IRuntimeProfilingStore store = new InMemoryRuntimeProfilingStore();</code></example>
internal sealed class InMemoryRuntimeProfilingStore : IRuntimeProfilingStore
{
    private readonly object sync;
    private Guid? maintenanceGate;
    internal Func<CancellationToken, Task<IResult<ProfilingClearResult>>> SharedClear { get; set; }
    internal Func<Guid, bool> OperationNodeReferenced { get; set; }
    internal Action RootDeleted { get; set; }
    private readonly Dictionary<Guid, RuntimeProfilingSession> sessions = [];
    private readonly Dictionary<string, Guid> sessionKeys = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> invalidSessionIds = [];
    private readonly HashSet<string> invalidSessionKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ProfilingNode> nodes = [];
    private readonly Dictionary<NodeCorrelationKey, Guid> nodeCorrelations = [];
    private readonly Dictionary<
        (Guid SessionId, Guid NodeId),
        RuntimeProfilingNodeParticipation
    > participations = [];
    private readonly Dictionary<
        (Guid SessionId, Guid NodeId),
        RuntimeProfilingContext
    > runtimeContexts = [];
    private readonly Dictionary<Guid, RuntimeProfilingSnapshot> snapshots = [];
    private readonly Dictionary<string, Guid> snapshotKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ProfilingMarker> markers = [];
    private readonly Dictionary<Guid, ProfilingSegment> segments = [];
    private readonly Dictionary<Guid, ProfilingMetricObservation> metricObservations = [];

    public InMemoryRuntimeProfilingStore() : this(new object())
    {
    }

    internal InMemoryRuntimeProfilingStore(object synchronization)
    {
        this.sync = synchronization ?? new object();
    }

    internal Result<ProfilingNode> RegisterOperationNode(ProfilingNode proposed, bool validateOnly = false)
    {
        lock (this.sync)
        {
            if (this.nodes.TryGetValue(proposed.Identity.Id, out var existing))
            {
                return SameProcess(existing, proposed)
                    ? Result<ProfilingNode>.Success(existing)
                    : Result<ProfilingNode>.Failure(new ProfilingValidationError("The cached profiling process descriptor conflicts with its identity."));
            }

            if (this.nodes.Values.Any(node => node.Identity.Key == proposed.Identity.Key))
            {
                return Result<ProfilingNode>.Failure(new ProfilingValidationError("The profiling node key is already in use."));
            }

            if (validateOnly)
            {
                return Result<ProfilingNode>.Success(proposed);
            }

            var node = Clone(proposed);
            this.nodes.Add(node.Identity.Id, node);
            return Result<ProfilingNode>.Success(node);
        }
    }

    internal void ReleaseSharedNode(Guid id, bool operationReferenced)
    {
        lock (this.sync)
        {
            if (operationReferenced || this.participations.Values.Any(value => value.NodeId == id)
                || this.runtimeContexts.Values.Any(value => value.NodeId == id) || this.snapshots.Values.Any(value => value.NodeId == id)
                || this.segments.Values.Any(value => value.NodeId == id) || this.markers.Values.Any(value => value.NodeId == id)
                || this.metricObservations.Values.Any(value => value.NodeId == id))
            {
                return;
            }

            this.nodes.Remove(id);
            RemoveValuesWhere(this.nodeCorrelations, nodeId => nodeId == id);
        }
    }

    private static bool SameProcess(ProfilingNode left, ProfilingNode right) => left.Identity == right.Identity
        && left.HostName == right.HostName && left.ProcessId == right.ProcessId && left.ProcessStartedUtc == right.ProcessStartedUtc
        && left.ApplicationVersion == right.ApplicationVersion && left.DisplayName == right.DisplayName;

    internal bool HasActiveSession { get { lock (this.sync) { return this.sessions.Values.Any(IsActive); } } }

    internal bool ReserveMaintenance(Guid id)
    {
        lock (this.sync)
        {
            if (this.maintenanceGate.HasValue || this.sessions.Values.Any(IsActive))
            {
                return false;
            }

            this.maintenanceGate = id;
            return true;
        }
    }

    internal void ReleaseMaintenance(Guid id)
    {
        lock (this.sync)
        {
            if (this.maintenanceGate == id)
            {
                this.maintenanceGate = null;
            }
        }
    }

    internal (int Sessions, long Snapshots, bool Remaining) DeleteForClear(Guid id, ProfilingClearRequest selection, int maximumRoots)
    {
        lock (this.sync)
        {
            if (this.maintenanceGate != id)
            {
                throw new InvalidOperationException("A Runtime clear requires its reserved maintenance gate.");
            }

            var candidates = this.sessions.Values.Where(s => IsTerminal(s.State) && ProfilingClearCoordinator.InRange(selection, TerminalTimestamp(s)))
                .OrderBy(s => TerminalTimestamp(s)).ThenBy(s => s.Identity.Id.ToString("N"), StringComparer.Ordinal).Take(maximumRoots + 1).ToArray();
            long snapshotsRemoved = 0;
            foreach (var session in candidates.Take(maximumRoots))
            {
                snapshotsRemoved += this.snapshots.Values.LongCount(snapshot => snapshot.SessionId == session.Identity.Id);
                this.DeleteSession(session);
            }

            return (Math.Min(candidates.Length, maximumRoots), snapshotsRemoved, candidates.Length > maximumRoots);
        }
    }

    /// <inheritdoc />
    public ProfilingStoreCapabilities Capabilities { get; } = new(false);

    /// <inheritdoc />
    public Task<IResult<RuntimeProfilingSessionResolution>> GetOrCreateActiveSessionAsync(
        RuntimeProfilingSessionCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (this.maintenanceGate.HasValue)
            {
                return Failure<RuntimeProfilingSessionResolution>(new ProfilingBusyError("Runtime history maintenance is in progress."));
            }

            var active = this.sessions.Values.SingleOrDefault(IsActive);
            if (active is not null)
            {
                return Success(new RuntimeProfilingSessionResolution(Clone(active), false));
            }

            var validation = ValidateSessionRequest(request);
            if (validation is not null)
            {
                return Failure<RuntimeProfilingSessionResolution>(validation);
            }

            if (
                this.invalidSessionIds.Contains(request.Identity.Id)
                || this.invalidSessionKeys.Contains(request.Identity.Key)
            )
            {
                return Failure<RuntimeProfilingSessionResolution>(
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
                return Failure<RuntimeProfilingSessionResolution>(
                    new ProfilingValidationError(
                        "The profiling session identity is already in use."
                    )
                );
            }

            var session = new RuntimeProfilingSession
            {
                Identity = request.Identity,
                Name = NormalizeOptional(request.Name),
                State = RuntimeProfilingSessionState.Running,
                StartedUtc = request.StartedUtc,
                EndsUtc = request.StartedUtc.Add(request.Duration),
                SamplingInterval = request.SamplingInterval,
                Duration = request.Duration,
                Tags = CloneStrings(request.Tags),
            };

            this.sessions.Add(session.Identity.Id, session);
            this.sessionKeys.Add(session.Identity.Key, session.Identity.Id);

            return Success(new RuntimeProfilingSessionResolution(Clone(session), true));
        }
    }

    /// <inheritdoc />
    public Task<IResult<RuntimeProfilingSession>> GetActiveSessionAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var session = this.sessions.Values.SingleOrDefault(IsActive);
            return session is null
                ? Failure<RuntimeProfilingSession>(
                    new ProfilingInvalidStateError("No profiling session is active.")
                )
                : Success(Clone(session));
        }
    }

    /// <inheritdoc />
    public Task<IResult<RuntimeProfilingSession>> FindSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!IsPublicKey(sessionKey))
            {
                return Failure<RuntimeProfilingSession>(new ProfilingInvalidKeyError("session"));
            }

            return this.TryGetSession(sessionKey, out var session)
                ? Success(Clone(session))
                : Failure<RuntimeProfilingSession>(
                    new NotFoundError($"Profiling session '{sessionKey}' was not found.")
                );
        }
    }

    /// <inheritdoc />
    public Task<IResult<IReadOnlyList<RuntimeProfilingSession>>> ListSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            return Success<IReadOnlyList<RuntimeProfilingSession>>(
                this.sessions.Values.OrderByDescending(x => x.StartedUtc).Select(Clone).ToArray()
            );
        }
    }

    /// <inheritdoc />
    public Task<IResult<RuntimeProfilingSession>> UpdateSessionMetadataAsync(
        string sessionKey,
        RuntimeProfilingSessionMetadata metadata,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (metadata is null)
            {
                return Failure<RuntimeProfilingSession>(
                    new ProfilingValidationError("Session metadata is required.")
                );
            }

            if (!this.TryGetSession(sessionKey, out var session))
            {
                return Failure<RuntimeProfilingSession>(
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
    public Task<IResult<RuntimeProfilingSession>> TryTransitionSessionAsync(
        Guid sessionId,
        IReadOnlyCollection<RuntimeProfilingSessionState> expectedStates,
        RuntimeProfilingSessionState nextState,
        DateTimeOffset transitionedUtc,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.sessions.TryGetValue(sessionId, out var session))
            {
                return Failure<RuntimeProfilingSession>(
                    new NotFoundError("The profiling session was not found.")
                );
            }

            if (expectedStates?.Contains(session.State) != true)
            {
                return Failure<RuntimeProfilingSession>(
                    new ProfilingInvalidStateError(
                        $"The session cannot transition from '{session.State}'."
                    )
                );
            }

            if (!IsValidTransition(session.State, nextState))
            {
                return Failure<RuntimeProfilingSession>(
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
    public Task<IResult<ProfilingNode>> FindNodeAsync(RuntimeProfilingNodeCorrelation correlation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            var node = correlation is not null && this.nodeCorrelations.TryGetValue(NodeCorrelationKey.Create(correlation), out var id)
                && this.nodes.TryGetValue(id, out var found) ? Clone(found) : null;
            return Success(node);
        }
    }

    /// <inheritdoc />
    public Task<IResult<ProfilingNode>> GetOrCreateNodeAsync(
        RuntimeProfilingNodeCorrelation correlation,
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

            if (this.nodes.TryGetValue(proposedNode.Identity.Id, out var operationNode) && operationNode.Correlation is null && SameProcess(operationNode, proposedNode))
            {
                var attached = operationNode with { Correlation = correlation };
                this.nodes[attached.Identity.Id] = attached;
                this.nodeCorrelations.Add(correlationKey, attached.Identity.Id);
                return Success(Clone(attached));
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
    public Task<IResult<RuntimeProfilingNodeParticipation>> UpsertParticipationAsync(
        RuntimeProfilingNodeParticipation participation,
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
                return Failure<RuntimeProfilingNodeParticipation>(validation);
            }

            if (
                participation.SuccessfulCaptureCount < 0
                || participation.SkippedCaptureCount < 0
                || participation.FailedCaptureCount < 0
            )
            {
                return Failure<RuntimeProfilingNodeParticipation>(
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
                    return Failure<RuntimeProfilingNodeParticipation>(
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
    public Task<IResult<RuntimeProfilingContext>> AddRuntimeContextAsync(
        RuntimeProfilingContext context,
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
                return Failure<RuntimeProfilingContext>(validation);
            }

            var key = (context.SessionId, context.NodeId);
            if (this.runtimeContexts.TryGetValue(key, out var existing))
            {
                return existing == context
                    ? Success(Clone(existing))
                    : Failure<RuntimeProfilingContext>(
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
    public Task<IResult<RuntimeProfilingSnapshot>> AddSnapshotAsync(
        RuntimeProfilingSnapshot snapshot,
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
                return Failure<RuntimeProfilingSnapshot>(validation);
            }

            if (!this.IsInsideCollectionWindow(snapshot.SessionId, snapshot.TimestampUtc))
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

            if (this.snapshots.TryGetValue(snapshot.Identity.Id, out var existing))
            {
                return existing == snapshot
                    ? Success(existing)
                    : Failure<RuntimeProfilingSnapshot>(
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
                return Failure<RuntimeProfilingSnapshot>(
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
    public Task<IResult<ProfilingMarker>> AddMarkerAsync(
        ProfilingMarker marker,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (marker is not null && marker.Scope == ProfilingMarkerScope.Node)
            {
                var referenceError = this.ValidateSessionNodeRecord(marker.SessionId, marker.SessionKey, marker.NodeId ?? Guid.Empty, marker.NodeKey);
                if (referenceError is not null)
                {
                    return Failure<ProfilingMarker>(referenceError);
                }
            }

            if (
                marker is null
                || !this.sessions.TryGetValue(marker.SessionId, out var session)
                || !string.Equals(session.Identity.Key, marker.SessionKey, StringComparison.Ordinal)
            )
            {
                return Failure<ProfilingMarker>(
                    new NotFoundError("The active profiling session was not found.")
                );
            }

            if (
                !IsActive(session)
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

            if (string.IsNullOrWhiteSpace(marker.Name) || marker.Name.Trim().Length > 100)
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

            var normalized = marker with { Name = marker.Name.Trim() };
            return this.AddImmutable(this.markers, normalized.Id, normalized, "marker");
        }
    }

    /// <inheritdoc />
    public Task<IResult<ProfilingSegment>> UpsertSegmentAsync(
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
                    || segment.Outcome != null
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
                existing.Outcome != null
                || normalized.Outcome == null
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
    public Task<IResult<ProfilingMetricObservation>> AddMetricObservationAsync(
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
    public Task<IResult<RuntimeProfilingSessionData>> GetSessionDataAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (!this.TryGetSession(sessionKey, out var session))
            {
                return Failure<RuntimeProfilingSessionData>(
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
                new RuntimeProfilingSessionData
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
                    Markers = this
                        .markers.Values.Where(x => x.SessionId == sessionId)
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
    public Task<IResult<RuntimeProfilingSession>> ImportSessionAsync(
        RuntimeProfilingSessionData data,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            if (this.maintenanceGate.HasValue)
            {
                return Failure<RuntimeProfilingSession>(new ProfilingBusyError("Runtime history maintenance is in progress."));
            }

            var validation = this.ValidateImportData(data);
            if (validation is not null)
            {
                return Failure<RuntimeProfilingSession>(validation);
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

            foreach (var marker in data.Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Session))
            {
                this.markers.Add(marker.Id, marker);
            }

            foreach (var marker in data.Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Node))
            {
                this.markers.Add(marker.Id, marker);
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
    public Task<IResult<bool>> DeleteSessionAsync(
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
    public Task<IResult<int>> DeleteUnpinnedSessionsAsync(
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
    public Task<IResult<ProfilingClearResult>> ClearAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (this.SharedClear is not null)
        {
            return this.SharedClear(cancellationToken);
        }

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
            this.markers.Clear();
            this.segments.Clear();
            this.metricObservations.Clear();

            return Success(result);
        }
    }

    /// <inheritdoc />
    public Task<IResult<int>> ApplyRetentionAsync(
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

    private static Task<IResult<T>> Success<T>(T value) => Task.FromResult<IResult<T>>(Result<T>.Success(value));

    private static Task<IResult<T>> Failure<T>(IResultError error) =>
        Task.FromResult<IResult<T>>(Result<T>.Failure().WithError(error));

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

    private IResultError ValidateImportData(RuntimeProfilingSessionData data)
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
            || data.Markers.SafeNull().Where(marker => marker.Scope == ProfilingMarkerScope.Session).Any(item =>
                item is null
                || item.Id == Guid.Empty
                || this.markers.ContainsKey(item.Id)
                || !HasValidSession(item.SessionId, item.SessionKey)
            )
            || data.Markers.SafeNull().Where(marker => marker.Scope == ProfilingMarkerScope.Node).Any(item =>
                item is null
                || item.Id == Guid.Empty
                || this.markers.ContainsKey(item.Id)
                || !HasValidSession(item.SessionId, item.SessionKey)
                || !HasValidNode(item.NodeId ?? Guid.Empty, item.NodeKey)
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
            || data.Markers.SafeNull().Where(marker => marker.Scope == ProfilingMarkerScope.Session).Select(item => item.Id).Distinct().Count()
                != data.Markers.SafeNull().Where(marker => marker.Scope == ProfilingMarkerScope.Session).Count()
            || data.Markers.SafeNull().Where(marker => marker.Scope == ProfilingMarkerScope.Node).Select(item => item.Id).Distinct().Count()
                != data.Markers.SafeNull().Where(marker => marker.Scope == ProfilingMarkerScope.Node).Count()
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

    private bool TryGetSession(string sessionKey, out RuntimeProfilingSession session)
    {
        session = null;
        return IsPublicKey(sessionKey)
            && this.sessionKeys.TryGetValue(sessionKey, out var sessionId)
            && this.sessions.TryGetValue(sessionId, out session);
    }

    private Task<IResult<T>> AddImmutable<T>(
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

    private void DeleteSession(RuntimeProfilingSession session)
    {
        var sessionId = session.Identity.Id;
        var referencedNodes = this.participations.Values.Where(value => value.SessionId == sessionId).Select(value => value.NodeId)
            .Concat(this.runtimeContexts.Values.Where(value => value.SessionId == sessionId).Select(value => value.NodeId))
            .Concat(this.snapshots.Values.Where(value => value.SessionId == sessionId).Select(value => value.NodeId))
            .Concat(this.segments.Values.Where(value => value.SessionId == sessionId).Select(value => value.NodeId))
            .Concat(this.metricObservations.Values.Where(value => value.SessionId == sessionId).Select(value => value.NodeId))
            .Concat(this.markers.Values.Where(value => value.SessionId == sessionId && value.NodeId.HasValue).Select(value => value.NodeId.Value)).ToHashSet();
        this.invalidSessionIds.Add(sessionId);
        this.invalidSessionKeys.Add(session.Identity.Key);
        this.sessions.Remove(sessionId);
        this.sessionKeys.Remove(session.Identity.Key);

        RemoveKeysWhere(this.participations, key => key.SessionId == sessionId);
        RemoveKeysWhere(this.runtimeContexts, key => key.SessionId == sessionId);
        RemoveValuesWhere(this.markers, value => value.SessionId == sessionId);
        RemoveValuesWhere(this.segments, value => value.SessionId == sessionId);
        RemoveValuesWhere(this.metricObservations, value => value.SessionId == sessionId);

        foreach (
            var snapshot in this.snapshots.Values.Where(x => x.SessionId == sessionId).ToArray()
        )
        {
            this.snapshots.Remove(snapshot.Identity.Id);
            this.snapshotKeys.Remove(snapshot.Identity.Key);
        }

        foreach (var id in referencedNodes)
        {
            this.ReleaseSharedNode(id, this.OperationNodeReferenced?.Invoke(id) == true);
        }

        this.RootDeleted?.Invoke();
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

    private static bool IsActive(RuntimeProfilingSession session) =>
        session.State == RuntimeProfilingSessionState.Running;

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

    private static bool IsValidTransition(
        RuntimeProfilingSessionState current,
        RuntimeProfilingSessionState next
    ) => current == next || current == RuntimeProfilingSessionState.Running && IsTerminal(next);

    private static int ParticipationRank(RuntimeProfilingParticipationState state) =>
        state switch
        {
            RuntimeProfilingParticipationState.Accepted => 0,
            RuntimeProfilingParticipationState.Collecting => 1,
            _ => 2,
        };

    private static DateTimeOffset TerminalTimestamp(RuntimeProfilingSession session) =>
        session.CompletedUtc ?? session.EndsUtc;

    private static bool IsPublicKey(string value) =>
        value?.Length == 8
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9');

    private static string NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> CloneStrings(IEnumerable<string> values) =>
        values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() ?? [];

    private static RuntimeProfilingSession Clone(RuntimeProfilingSession value) =>
        value with
        {
            Tags = CloneStrings(value.Tags),
        };

    private static ProfilingNode Clone(ProfilingNode value) =>
        value with
        {
            Correlation = value.Correlation is null ? null : value.Correlation with { },
        };

    private static RuntimeProfilingNodeParticipation Clone(RuntimeProfilingNodeParticipation value) =>
        value with
        { };

    private static RuntimeProfilingContext Clone(RuntimeProfilingContext value) => value with { };

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
        public static NodeCorrelationKey Create(RuntimeProfilingNodeCorrelation correlation) =>
            new(correlation.BroadcastNodeIdentity.Trim(), correlation.ProcessStartedUtc.UtcTicks);
    }
}
