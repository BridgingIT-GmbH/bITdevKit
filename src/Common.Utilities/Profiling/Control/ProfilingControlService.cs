// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Provides the single programmatic control path for deployment-wide profiling operations.
/// </summary>
/// <param name="options">The shared profiling configuration.</param>
/// <param name="timeProvider">The UTC clock.</param>
/// <param name="store">The selected profiling store when the feature is enabled.</param>
/// <param name="broadcasts">Profiling's adapter over the existing typed Broadcast service.</param>
/// <param name="nodes">The stable profiling node provider.</param>
/// <param name="broadcastingOptions">The shared Broadcast scopes and availability.</param>
/// <example><code>var result = await control.StartAsync(new ProfilingStartRequest("warm-up"));</code></example>
public sealed class ProfilingControlService(
    ProfilingOptions options,
    TimeProvider timeProvider,
    IProfilingStore store = null,
    IProfilingBroadcastService broadcasts = null,
    IProfilingNodeIdentityProvider nodes = null,
    BroadcastingOptions broadcastingOptions = null
) : IProfilingControlService
{
    /// <inheritdoc />
    public async Task<Result<ProfilingStatus>> GetStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!options.Enabled)
        {
            return Result<ProfilingStatus>.Success(new(false, false, null, []));
        }

        if (!this.IsAvailable)
        {
            return Result<ProfilingStatus>.Success(new(true, false, null, []));
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (activeResult.IsFailure)
        {
            return IsNoActiveSession(activeResult)
                ? Result<ProfilingStatus>.Success(new(true, true, null, []))
                : CopyFailure<ProfilingStatus, ProfilingSession>(activeResult);
        }

        var dataResult = await store
            .GetSessionDataAsync(activeResult.Value.Identity.Key, cancellationToken)
            .ConfigureAwait(false);
        return dataResult.IsSuccess
            ? Result<ProfilingStatus>.Success(
                new(true, true, activeResult.Value, dataResult.Value.Participations)
            )
            : CopyFailure<ProfilingStatus, ProfilingSessionData>(dataResult);
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingControlResult>> StartAsync(
        ProfilingStartRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<ProfilingControlResult>(operationalError);
        }

        var requestError = ValidateStartRequest(request);
        if (requestError is not null)
        {
            return Failure<ProfilingControlResult>(requestError);
        }

        var interval = request.SamplingInterval ?? options.SamplingInterval;
        var duration = request.Duration ?? options.Duration;
        if (interval < ProfilingOptions.MinimumSamplingInterval || duration <= TimeSpan.Zero)
        {
            return Failure<ProfilingControlResult>(
                new ProfilingValidationError(
                    "A sampling interval of at least 500 ms and a positive duration are required."
                )
            );
        }

        var preparedResult = await this.PrepareTargetsAsync(
                requireTargets: true,
                validateStoreCapability: true,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (preparedResult.IsFailure)
        {
            return CopyFailure<ProfilingControlResult, PreparedTargets>(preparedResult);
        }

        var now = timeProvider.GetUtcNow();
        var createResult = await store
            .GetOrCreateActiveSessionAsync(
                new(
                    ProfilingSessionIdentity.Create(),
                    NormalizeName(request.Name, now),
                    now,
                    interval,
                    duration,
                    NormalizeTags(request.Tags)
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (createResult.IsFailure)
        {
            return CopyFailure<ProfilingControlResult, ProfilingSessionResolution>(createResult);
        }

        var resolution = createResult.Value;
        if (!resolution.Created)
        {
            return Result<ProfilingControlResult>.Success(new(resolution.Session, false, []));
        }

        Result<BroadcastResult> publicationResult;
        try
        {
            publicationResult = await broadcasts
                .PublishAsync(
                    new ProfilingStartBroadcast(ProfilingSessionBroadcast.From(resolution.Session)),
                    preparedResult.Value.Snapshot,
                    new()
                    {
                        Lifetime = options.ParticipationDeadline,
                        RequireAtLeastOneTarget = true,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await this.FailCreatedSessionAsync(resolution.Session).ConfigureAwait(false);
            throw;
        }

        if (publicationResult.IsFailure)
        {
            await this.FailCreatedSessionAsync(resolution.Session).ConfigureAwait(false);
            return CopyFailure<ProfilingControlResult, BroadcastResult>(publicationResult);
        }

        var participantResult = await this.RecordExpectedParticipantsAsync(
                resolution.Session,
                publicationResult.Value,
                preparedResult.Value.Nodes,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (participantResult.IsFailure)
        {
            return CopyFailure<ProfilingControlResult, bool>(participantResult);
        }

        return Result<ProfilingControlResult>.Success(
            this.CreateControlResult(
                resolution.Session,
                true,
                publicationResult.Value,
                preparedResult.Value.Nodes
            )
        );
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingControlResult>> StopAsync(
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<ProfilingControlResult>(operationalError);
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (activeResult.IsFailure)
        {
            return CopyFailure<ProfilingControlResult, ProfilingSession>(activeResult);
        }

        var preparedResult = await this.PrepareTargetsAsync(
                requireTargets: false,
                validateStoreCapability: false,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (preparedResult.IsFailure)
        {
            return CopyFailure<ProfilingControlResult, PreparedTargets>(preparedResult);
        }

        var transitionedResult = await store
            .TryTransitionSessionAsync(
                activeResult.Value.Identity.Id,
                [ProfilingSessionState.Running],
                ProfilingSessionState.Stopped,
                timeProvider.GetUtcNow(),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (transitionedResult.IsFailure)
        {
            return CopyFailure<ProfilingControlResult, ProfilingSession>(transitionedResult);
        }

        var publicationResult = await broadcasts
            .PublishAsync(
                new ProfilingStopBroadcast(
                    activeResult.Value.Identity.Id,
                    activeResult.Value.Identity.Key
                ),
                preparedResult.Value.Snapshot,
                new() { Lifetime = options.ParticipationDeadline },
                cancellationToken
            )
            .ConfigureAwait(false);
        return publicationResult.IsSuccess
            ? Result<ProfilingControlResult>.Success(
                this.CreateControlResult(
                    transitionedResult.Value,
                    false,
                    publicationResult.Value,
                    preparedResult.Value.Nodes
                )
            )
            : CopyFailure<ProfilingControlResult, BroadcastResult>(publicationResult);
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingControlResult>> SnapshotAsync(
        string standaloneSessionName = null,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<ProfilingControlResult>(operationalError);
        }

        var preparedResult = await this.PrepareTargetsAsync(
                requireTargets: true,
                validateStoreCapability: true,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (preparedResult.IsFailure)
        {
            return CopyFailure<ProfilingControlResult, PreparedTargets>(preparedResult);
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        ProfilingSession session;
        var standalone = false;
        if (activeResult.IsSuccess)
        {
            session = activeResult.Value;
        }
        else if (IsNoActiveSession(activeResult))
        {
            var now = timeProvider.GetUtcNow();
            var createResult = await store
                .GetOrCreateActiveSessionAsync(
                    new(
                        ProfilingSessionIdentity.Create(),
                        NormalizeManualSnapshotName(standaloneSessionName, now),
                        now,
                        options.SamplingInterval,
                        options.ParticipationDeadline,
                        []
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (createResult.IsFailure)
            {
                return CopyFailure<ProfilingControlResult, ProfilingSessionResolution>(
                    createResult
                );
            }

            session = createResult.Value.Session;
            standalone = createResult.Value.Created;
        }
        else
        {
            return CopyFailure<ProfilingControlResult, ProfilingSession>(activeResult);
        }

        Result<BroadcastResult> publicationResult;
        try
        {
            publicationResult = await broadcasts
                .PublishAsync(
                    new ProfilingSnapshotBroadcast(
                        ProfilingSessionBroadcast.From(session),
                        standalone
                            ? ProfilingNodeRole.ExpectedParticipant
                            : ProfilingNodeRole.AdHocContributor
                    ),
                    preparedResult.Value.Snapshot,
                    new()
                    {
                        Lifetime = options.ParticipationDeadline,
                        RequireAtLeastOneTarget = true,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (standalone)
            {
                await this.FailCreatedSessionAsync(session).ConfigureAwait(false);
            }

            throw;
        }

        if (publicationResult.IsFailure)
        {
            if (standalone)
            {
                await this.FailCreatedSessionAsync(session).ConfigureAwait(false);
            }

            return CopyFailure<ProfilingControlResult, BroadcastResult>(publicationResult);
        }

        if (standalone)
        {
            var participantResult = await this.RecordExpectedParticipantsAsync(
                    session,
                    publicationResult.Value,
                    preparedResult.Value.Nodes,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (participantResult.IsFailure)
            {
                return CopyFailure<ProfilingControlResult, bool>(participantResult);
            }

            var terminalResult = await store
                .TryTransitionSessionAsync(
                    session.Identity.Id,
                    [ProfilingSessionState.Running],
                    publicationResult.Value.AcceptedCount > 0
                        ? ProfilingSessionState.Completed
                        : ProfilingSessionState.Failed,
                    timeProvider.GetUtcNow(),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (terminalResult.IsFailure)
            {
                return CopyFailure<ProfilingControlResult, ProfilingSession>(terminalResult);
            }

            session = terminalResult.Value;
        }

        return Result<ProfilingControlResult>.Success(
            this.CreateControlResult(
                session,
                standalone,
                publicationResult.Value,
                preparedResult.Value.Nodes
            )
        );
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingControlResult>> CollectGarbageAsync(
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<ProfilingControlResult>(operationalError);
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        var session = activeResult.IsSuccess ? activeResult.Value : null;
        if (activeResult.IsFailure && !IsNoActiveSession(activeResult))
        {
            return CopyFailure<ProfilingControlResult, ProfilingSession>(activeResult);
        }

        var preparedResult = await this.PrepareTargetsAsync(
                requireTargets: false,
                validateStoreCapability: false,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (preparedResult.IsFailure)
        {
            return CopyFailure<ProfilingControlResult, PreparedTargets>(preparedResult);
        }

        var publicationResult = await broadcasts
            .PublishAsync(
                new ProfilingGarbageCollectionBroadcast(
                    session?.Identity.Id ?? Guid.Empty,
                    session?.Identity.Key
                ),
                preparedResult.Value.Snapshot,
                new() { Lifetime = options.ParticipationDeadline },
                cancellationToken
            )
            .ConfigureAwait(false);
        return publicationResult.IsSuccess
            ? Result<ProfilingControlResult>.Success(
                this.CreateControlResult(
                    session,
                    false,
                    publicationResult.Value,
                    preparedResult.Value.Nodes
                )
            )
            : CopyFailure<ProfilingControlResult, BroadcastResult>(publicationResult);
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingPhaseMarker>> AddPhaseMarkerAsync(
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<ProfilingPhaseMarker>(operationalError);
        }

        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 100)
        {
            return Failure<ProfilingPhaseMarker>(
                new ProfilingValidationError(
                    "A phase marker name of at most 100 characters is required."
                )
            );
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (activeResult.IsFailure)
        {
            return CopyFailure<ProfilingPhaseMarker, ProfilingSession>(activeResult);
        }

        return await store
            .AddPhaseMarkerAsync(
                new(
                    Guid.NewGuid(),
                    activeResult.Value.Identity.Id,
                    activeResult.Value.Identity.Key,
                    normalizedName,
                    timeProvider.GetUtcNow()
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingControlResult>> RestartAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<ProfilingControlResult>(operationalError);
        }

        var sourceResult = await store
            .FindSessionAsync(sessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (sourceResult.IsFailure)
        {
            return CopyFailure<ProfilingControlResult, ProfilingSession>(sourceResult);
        }

        if (sourceResult.Value.State == ProfilingSessionState.Running)
        {
            var stopResult = await this.StopAsync(cancellationToken).ConfigureAwait(false);
            if (stopResult.IsFailure)
            {
                return stopResult;
            }
        }

        var now = timeProvider.GetUtcNow();
        var baseName = string.IsNullOrWhiteSpace(sourceResult.Value.Name)
            ? sourceResult.Value.Identity.Key
            : sourceResult.Value.Name.Trim();
        return await this.StartAsync(
                new(
                    $"{baseName} — restart {now.ToString("O", CultureInfo.InvariantCulture)}",
                    sourceResult.Value.SamplingInterval,
                    sourceResult.Value.Duration,
                    sourceResult.Value.Tags
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        return operationalError is null
            ? store.DeleteSessionAsync(sessionKey, cancellationToken)
            : Task.FromResult(Failure<bool>(operationalError));
    }

    /// <inheritdoc />
    public Task<Result<int>> DeleteUnpinnedSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        return operationalError is null
            ? store.DeleteUnpinnedSessionsAsync(cancellationToken)
            : Task.FromResult(Failure<int>(operationalError));
    }

    /// <inheritdoc />
    public Task<Result<ProfilingClearResult>> ClearAsync(
        bool confirmed,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Task.FromResult(Failure<ProfilingClearResult>(operationalError));
        }

        return confirmed
            ? store.ClearAsync(cancellationToken)
            : Task.FromResult(
                Failure<ProfilingClearResult>(
                    new ProfilingValidationError(
                        "Clearing all profiling data requires explicit confirmation."
                    )
                )
            );
    }

    private bool IsAvailable =>
        store is not null
        && broadcasts is not null
        && nodes is not null
        && broadcastingOptions is not null
        && broadcastingOptions.Enabled;

    private IResultError GetOperationalError() =>
        !options.Enabled ? new ProfilingDisabledError()
        : !this.IsAvailable
            ? new ProfilingUnavailableError(
                "The profiling store or Broadcast integration is unavailable."
            )
        : null;

    private async Task<Result<PreparedTargets>> PrepareTargetsAsync(
        bool requireTargets,
        bool validateStoreCapability,
        CancellationToken cancellationToken
    )
    {
        if (
            options.ParticipationDeadline <= TimeSpan.Zero
            || options.ParticipationDeadline >= broadcastingOptions.DuplicateRetention
        )
        {
            return Failure<PreparedTargets>(
                new ProfilingValidationError(
                    "The participation deadline must be positive and shorter than Broadcast duplicate retention."
                )
            );
        }

        var snapshotResult = await broadcasts
            .PrepareTargetsAsync(broadcastingOptions.Scopes.ToArray(), cancellationToken)
            .ConfigureAwait(false);
        if (snapshotResult.IsFailure)
        {
            return CopyFailure<PreparedTargets, ProfilingBroadcastTargetSnapshot>(snapshotResult);
        }

        if (
            validateStoreCapability
            && snapshotResult.Value.TargetCount > 1
            && !store.Capabilities.SupportsMultiNode
        )
        {
            return Failure<PreparedTargets>(new ProfilingSharedStoreRequiredError());
        }

        if (requireTargets && snapshotResult.Value.TargetCount == 0)
        {
            return Failure<PreparedTargets>(new BroadcastNoTargetError());
        }

        var resolvedNodes = new Dictionary<string, ProfilingNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in snapshotResult.Value.Targets)
        {
            var nodeResult = await nodes.GetAsync(target, cancellationToken).ConfigureAwait(false);
            if (nodeResult.IsFailure)
            {
                return CopyFailure<PreparedTargets, ProfilingNode>(nodeResult);
            }

            resolvedNodes[target.NodeIdentity] = nodeResult.Value;
        }

        return Result<PreparedTargets>.Success(new(snapshotResult.Value, resolvedNodes));
    }

    private async Task<Result<bool>> RecordExpectedParticipantsAsync(
        ProfilingSession session,
        BroadcastResult publication,
        IReadOnlyDictionary<string, ProfilingNode> resolvedNodes,
        CancellationToken cancellationToken
    )
    {
        foreach (
            var delivery in publication.Nodes.Where(node =>
                node.Outcome == BroadcastDeliveryOutcome.Accepted
            )
        )
        {
            if (!resolvedNodes.TryGetValue(delivery.NodeIdentity, out var node))
            {
                return Failure<bool>(
                    new ProfilingUnavailableError(
                        "An accepted Broadcast node could not be resolved."
                    )
                );
            }

            var participation = new ProfilingNodeParticipation
            {
                SessionId = session.Identity.Id,
                SessionKey = session.Identity.Key,
                NodeId = node.Identity.Id,
                NodeKey = node.Identity.Key,
                Role = ProfilingNodeRole.ExpectedParticipant,
                State = ProfilingParticipationState.Accepted,
                JoinedUtc = timeProvider.GetUtcNow(),
            };
            var upsertResult = await store
                .UpsertParticipationAsync(participation, cancellationToken)
                .ConfigureAwait(false);
            if (upsertResult.IsSuccess)
            {
                continue;
            }

            var dataResult = await store
                .GetSessionDataAsync(session.Identity.Key, cancellationToken)
                .ConfigureAwait(false);
            var existing = dataResult.IsSuccess
                ? dataResult.Value.Participations.FirstOrDefault(item =>
                    item.NodeId == node.Identity.Id
                    && item.Role == ProfilingNodeRole.ExpectedParticipant
                )
                : null;
            if (existing is null)
            {
                return CopyFailure<bool, ProfilingNodeParticipation>(upsertResult);
            }
        }

        return Result<bool>.Success(true);
    }

    private ProfilingControlResult CreateControlResult(
        ProfilingSession session,
        bool created,
        BroadcastResult publication,
        IReadOnlyDictionary<string, ProfilingNode> resolvedNodes
    ) =>
        new(
            session,
            created,
            publication
                .Nodes.Select(delivery => new ProfilingNodeOutcome(
                    resolvedNodes.TryGetValue(delivery.NodeIdentity, out var node)
                        ? node.Identity.Key
                        : null,
                    delivery.Outcome,
                    delivery.Detail,
                    delivery.Duration
                ))
                .ToArray()
        );

    private async Task FailCreatedSessionAsync(ProfilingSession session)
    {
        await store
            .TryTransitionSessionAsync(
                session.Identity.Id,
                [ProfilingSessionState.Running],
                ProfilingSessionState.Failed,
                timeProvider.GetUtcNow(),
                CancellationToken.None
            )
            .ConfigureAwait(false);
    }

    private static string NormalizeName(string name, DateTimeOffset now) =>
        string.IsNullOrWhiteSpace(name)
            ? now.ToString(ProfilingOptions.DefaultSessionNameFormat, CultureInfo.InvariantCulture)
            : name.Trim();

    private static string NormalizeManualSnapshotName(string name, DateTimeOffset now) =>
        string.IsNullOrWhiteSpace(name)
            ? $"Manual snapshot"
            : name.Trim();

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags) =>
        (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IResultError ValidateStartRequest(ProfilingStartRequest request) =>
        request is null ? new ProfilingValidationError("A profiling start request is required.")
        : request.SamplingInterval is { } interval
        && interval < ProfilingOptions.MinimumSamplingInterval
            ? new ProfilingValidationError(
                "The profiling sampling interval must be at least 500 ms."
            )
        : request.Duration is { } duration && duration <= TimeSpan.Zero
            ? new ProfilingValidationError("The profiling duration must be positive.")
        : null;

    private static bool IsNoActiveSession(Result<ProfilingSession> result) =>
        result.Errors.Any(error => error is ProfilingInvalidStateError);

    private static Result<T> Failure<T>(IResultError error) => Result<T>.Failure().WithError(error);

    private static Result<TTarget> CopyFailure<TTarget, TSource>(Result<TSource> source) =>
        Result<TTarget>.Failure().WithErrors(source.Errors).WithMessages(source.Messages);

    private sealed record PreparedTargets(
        ProfilingBroadcastTargetSnapshot Snapshot,
        IReadOnlyDictionary<string, ProfilingNode> Nodes
    );
}
