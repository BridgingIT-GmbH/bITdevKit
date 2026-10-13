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
/// <example><code>var result = await control.StartAsync(new RuntimeProfilingStartRequest("warm-up"));</code></example>
public sealed class RuntimeProfilingControlService(
    ProfilingOptions options,
    TimeProvider timeProvider,
    IRuntimeProfilingStore store = null,
    IRuntimeProfilingBroadcastService broadcasts = null,
    IRuntimeProfilingNodeRegistrationAdapter nodes = null,
    BroadcastingOptions broadcastingOptions = null
) : IRuntimeProfilingControlService
{
    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingStatus>> GetStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!options.RuntimeEnabled)
        {
            return Result<RuntimeProfilingStatus>.Success(new(false, false, null, []));
        }

        if (!this.IsAvailable)
        {
            return Result<RuntimeProfilingStatus>.Success(new(true, false, null, []));
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (activeResult.IsFailure)
        {
            return IsNoActiveSession(activeResult)
                ? Result<RuntimeProfilingStatus>.Success(new(true, true, null, []))
                : CopyFailure<RuntimeProfilingStatus, RuntimeProfilingSession>(activeResult);
        }

        var dataResult = await store
            .GetSessionDataAsync(activeResult.Value.Identity.Key, cancellationToken)
            .ConfigureAwait(false);
        return dataResult.IsSuccess
            ? Result<RuntimeProfilingStatus>.Success(
                new(true, true, activeResult.Value, dataResult.Value.Participations)
            )
            : CopyFailure<RuntimeProfilingStatus, RuntimeProfilingSessionData>(dataResult);
    }

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingControlResult>> StartAsync(
        RuntimeProfilingStartRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<RuntimeProfilingControlResult>(operationalError);
        }

        var requestError = ValidateStartRequest(request);
        if (requestError is not null)
        {
            return Failure<RuntimeProfilingControlResult>(requestError);
        }

        var interval = request.SamplingInterval ?? options.Runtime.SamplingInterval;
        var duration = request.Duration ?? options.Runtime.Duration;
        if (interval < RuntimeProfilingOptions.MinimumSamplingInterval || duration <= TimeSpan.Zero)
        {
            return Failure<RuntimeProfilingControlResult>(
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
            return CopyFailure<RuntimeProfilingControlResult, PreparedTargets>(preparedResult);
        }

        var now = timeProvider.GetUtcNow();
        var createResult = await store
            .GetOrCreateActiveSessionAsync(
                new(
                    ProfilingIdentityFactory.CreateRuntimeSession(),
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
            return CopyFailure<RuntimeProfilingControlResult, RuntimeProfilingSessionResolution>(createResult);
        }

        var resolution = createResult.Value;
        if (!resolution.Created)
        {
            return Result<RuntimeProfilingControlResult>.Success(new(resolution.Session, false, []));
        }

        IResult<BroadcastResult> publicationResult;
        try
        {
            publicationResult = await broadcasts
                .PublishAsync(
                    new RuntimeProfilingStartBroadcast(RuntimeProfilingSessionBroadcast.From(resolution.Session)),
                    preparedResult.Value.Snapshot,
                    new()
                    {
                        Lifetime = options.Runtime.ParticipationDeadline,
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
            return CopyFailure<RuntimeProfilingControlResult, BroadcastResult>(publicationResult);
        }

        var participantResult = await this.RecordExpectedParticipantsAsync(
                resolution.Session,
                publicationResult.Value,
                preparedResult.Value,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (participantResult.IsFailure)
        {
            return CopyFailure<RuntimeProfilingControlResult, bool>(participantResult);
        }

        return Result<RuntimeProfilingControlResult>.Success(
            this.CreateControlResult(
                resolution.Session,
                true,
                publicationResult.Value,
                preparedResult.Value.Nodes
            )
        );
    }

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingControlResult>> StopAsync(
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<RuntimeProfilingControlResult>(operationalError);
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (activeResult.IsFailure)
        {
            return CopyFailure<RuntimeProfilingControlResult, RuntimeProfilingSession>(activeResult);
        }

        var preparedResult = await this.PrepareTargetsAsync(
                requireTargets: false,
                validateStoreCapability: false,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (preparedResult.IsFailure)
        {
            return CopyFailure<RuntimeProfilingControlResult, PreparedTargets>(preparedResult);
        }

        var transitionedResult = await store
            .TryTransitionSessionAsync(
                activeResult.Value.Identity.Id,
                [RuntimeProfilingSessionState.Running],
                RuntimeProfilingSessionState.Stopped,
                timeProvider.GetUtcNow(),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (transitionedResult.IsFailure)
        {
            return CopyFailure<RuntimeProfilingControlResult, RuntimeProfilingSession>(transitionedResult);
        }

        var publicationResult = await broadcasts
            .PublishAsync(
                new RuntimeProfilingStopBroadcast(
                    activeResult.Value.Identity.Id,
                    activeResult.Value.Identity.Key
                ),
                preparedResult.Value.Snapshot,
                new() { Lifetime = options.Runtime.ParticipationDeadline },
                cancellationToken
            )
            .ConfigureAwait(false);
        return publicationResult.IsSuccess
            ? Result<RuntimeProfilingControlResult>.Success(
                this.CreateControlResult(
                    transitionedResult.Value,
                    false,
                    publicationResult.Value,
                    preparedResult.Value.Nodes
                )
            )
            : CopyFailure<RuntimeProfilingControlResult, BroadcastResult>(publicationResult);
    }

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingControlResult>> SnapshotAsync(
        string standaloneSessionName = null,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<RuntimeProfilingControlResult>(operationalError);
        }

        var preparedResult = await this.PrepareTargetsAsync(
                requireTargets: true,
                validateStoreCapability: true,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (preparedResult.IsFailure)
        {
            return CopyFailure<RuntimeProfilingControlResult, PreparedTargets>(preparedResult);
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        RuntimeProfilingSession session;
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
                        ProfilingIdentityFactory.CreateRuntimeSession(),
                        NormalizeManualSnapshotName(standaloneSessionName, now),
                        now,
                        options.Runtime.SamplingInterval,
                        options.Runtime.ParticipationDeadline,
                        []
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (createResult.IsFailure)
            {
                return CopyFailure<RuntimeProfilingControlResult, RuntimeProfilingSessionResolution>(
                    createResult
                );
            }

            session = createResult.Value.Session;
            standalone = createResult.Value.Created;
        }
        else
        {
            return CopyFailure<RuntimeProfilingControlResult, RuntimeProfilingSession>(activeResult);
        }

        IResult<BroadcastResult> publicationResult;
        try
        {
            publicationResult = await broadcasts
                .PublishAsync(
                    new RuntimeProfilingSnapshotBroadcast(
                        RuntimeProfilingSessionBroadcast.From(session),
                        standalone
                            ? RuntimeProfilingNodeRole.ExpectedParticipant
                            : RuntimeProfilingNodeRole.AdHocContributor
                    ),
                    preparedResult.Value.Snapshot,
                    new()
                    {
                        Lifetime = options.Runtime.ParticipationDeadline,
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

            return CopyFailure<RuntimeProfilingControlResult, BroadcastResult>(publicationResult);
        }

        if (standalone)
        {
            var participantResult = await this.RecordExpectedParticipantsAsync(
                    session,
                    publicationResult.Value,
                    preparedResult.Value,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (participantResult.IsFailure)
            {
                return CopyFailure<RuntimeProfilingControlResult, bool>(participantResult);
            }

            var terminalResult = await store
                .TryTransitionSessionAsync(
                    session.Identity.Id,
                    [RuntimeProfilingSessionState.Running],
                    publicationResult.Value.AcceptedCount > 0
                        ? RuntimeProfilingSessionState.Completed
                        : RuntimeProfilingSessionState.Failed,
                    timeProvider.GetUtcNow(),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (terminalResult.IsFailure)
            {
                return CopyFailure<RuntimeProfilingControlResult, RuntimeProfilingSession>(terminalResult);
            }

            session = terminalResult.Value;
        }

        return Result<RuntimeProfilingControlResult>.Success(
            this.CreateControlResult(
                session,
                standalone,
                publicationResult.Value,
                preparedResult.Value.Nodes
            )
        );
    }

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingControlResult>> CollectGarbageAsync(
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<RuntimeProfilingControlResult>(operationalError);
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        var session = activeResult.IsSuccess ? activeResult.Value : null;
        if (activeResult.IsFailure && !IsNoActiveSession(activeResult))
        {
            return CopyFailure<RuntimeProfilingControlResult, RuntimeProfilingSession>(activeResult);
        }

        var preparedResult = await this.PrepareTargetsAsync(
                requireTargets: false,
                validateStoreCapability: false,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (preparedResult.IsFailure)
        {
            return CopyFailure<RuntimeProfilingControlResult, PreparedTargets>(preparedResult);
        }

        var publicationResult = await broadcasts
            .PublishAsync(
                new RuntimeProfilingGarbageCollectionBroadcast(
                    session?.Identity.Id ?? Guid.Empty,
                    session?.Identity.Key
                ),
                preparedResult.Value.Snapshot,
                new() { Lifetime = options.Runtime.ParticipationDeadline },
                cancellationToken
            )
            .ConfigureAwait(false);
        return publicationResult.IsSuccess
            ? Result<RuntimeProfilingControlResult>.Success(
                this.CreateControlResult(
                    session,
                    false,
                    publicationResult.Value,
                    preparedResult.Value.Nodes
                )
            )
            : CopyFailure<RuntimeProfilingControlResult, BroadcastResult>(publicationResult);
    }

    /// <inheritdoc />
    public async Task<IResult<ProfilingMarker>> AddMarkerAsync(
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<ProfilingMarker>(operationalError);
        }

        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 100)
        {
            return Failure<ProfilingMarker>(
                new ProfilingValidationError(
                    "A marker name of at most 100 characters is required."
                )
            );
        }

        var activeResult = await store
            .GetActiveSessionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (activeResult.IsFailure)
        {
            return CopyFailure<ProfilingMarker, RuntimeProfilingSession>(activeResult);
        }

        return await store
            .AddMarkerAsync(
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
    public async Task<IResult<RuntimeProfilingControlResult>> RestartAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Failure<RuntimeProfilingControlResult>(operationalError);
        }

        var sourceResult = await store
            .FindSessionAsync(sessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (sourceResult.IsFailure)
        {
            return CopyFailure<RuntimeProfilingControlResult, RuntimeProfilingSession>(sourceResult);
        }

        if (sourceResult.Value.State == RuntimeProfilingSessionState.Running)
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
    public Task<IResult<bool>> DeleteSessionAsync(
        string sessionKey,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        return operationalError is null
            ? store.DeleteSessionAsync(sessionKey, cancellationToken)
            : Task.FromResult<IResult<bool>>(Failure<bool>(operationalError));
    }

    /// <inheritdoc />
    public Task<IResult<int>> DeleteUnpinnedSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        return operationalError is null
            ? store.DeleteUnpinnedSessionsAsync(cancellationToken)
            : Task.FromResult<IResult<int>>(Failure<int>(operationalError));
    }

    /// <inheritdoc />
    public Task<IResult<ProfilingClearResult>> ClearAsync(
        bool confirmed,
        CancellationToken cancellationToken = default
    )
    {
        var operationalError = this.GetOperationalError();
        if (operationalError is not null)
        {
            return Task.FromResult<IResult<ProfilingClearResult>>(Failure<ProfilingClearResult>(operationalError));
        }

        return confirmed
            ? store.ClearAsync(cancellationToken)
            : Task.FromResult<IResult<ProfilingClearResult>>(
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
        !options.RuntimeEnabled ? new ProfilingDisabledError()
        : !this.IsAvailable
            ? new ProfilingUnavailableError(
                "The profiling store or Broadcast integration is unavailable."
            )
        : null;

    private async Task<IResult<PreparedTargets>> PrepareTargetsAsync(
        bool requireTargets,
        bool validateStoreCapability,
        CancellationToken cancellationToken
    )
    {
        if (
            options.Runtime.ParticipationDeadline <= TimeSpan.Zero
            || options.Runtime.ParticipationDeadline >= broadcastingOptions.DuplicateRetention
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
            return CopyFailure<PreparedTargets, RuntimeProfilingBroadcastTargetSnapshot>(snapshotResult);
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

            if (nodeResult.Value is not null)
            {
                resolvedNodes[target.NodeIdentity] = nodeResult.Value;
            }
        }

        return Result<PreparedTargets>.Success(new(snapshotResult.Value, resolvedNodes));
    }

    private async Task<IResult<bool>> RecordExpectedParticipantsAsync(
        RuntimeProfilingSession session,
        BroadcastResult publication,
        PreparedTargets targets,
        CancellationToken cancellationToken
    )
    {
        var resolvedNodes = targets.Nodes;
        foreach (
            var delivery in publication.Nodes.Where(node =>
                node.Outcome == BroadcastDeliveryOutcome.Accepted
            )
        )
        {
            if (!resolvedNodes.TryGetValue(delivery.NodeIdentity, out var node))
            {
                var registration = targets.Snapshot.Targets.FirstOrDefault(target => target.NodeIdentity == delivery.NodeIdentity);
                var deadline = timeProvider.GetUtcNow() + options.Runtime.ParticipationDeadline;
                do
                {
                    var result = await nodes.GetAsync(registration, cancellationToken).ConfigureAwait(false);
                    if (result.IsFailure)
                    {
                        return CopyFailure<bool, ProfilingNode>(result);
                    }

                    node = result.Value;
                    if (node is not null)
                    {
                        resolvedNodes[delivery.NodeIdentity] = node;
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(20), timeProvider, cancellationToken).ConfigureAwait(false);
                } while (timeProvider.GetUtcNow() < deadline);

                if (node is null)
                {
                    return Failure<bool>(new ProfilingUnavailableError("An accepted Broadcast node has not registered its profiling identity."));
                }
            }

            var participation = new RuntimeProfilingNodeParticipation
            {
                SessionId = session.Identity.Id,
                SessionKey = session.Identity.Key,
                NodeId = node.Identity.Id,
                NodeKey = node.Identity.Key,
                Role = RuntimeProfilingNodeRole.ExpectedParticipant,
                State = RuntimeProfilingParticipationState.Accepted,
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
                    && item.Role == RuntimeProfilingNodeRole.ExpectedParticipant
                )
                : null;
            if (existing is null)
            {
                return CopyFailure<bool, RuntimeProfilingNodeParticipation>(upsertResult);
            }
        }

        return Result<bool>.Success(true);
    }

    private RuntimeProfilingControlResult CreateControlResult(
        RuntimeProfilingSession session,
        bool created,
        BroadcastResult publication,
        IReadOnlyDictionary<string, ProfilingNode> resolvedNodes
    ) =>
        new(
            session,
            created,
            publication
                .Nodes.Select(delivery => new RuntimeProfilingNodeOutcome(
                    resolvedNodes.TryGetValue(delivery.NodeIdentity, out var node)
                        ? node.Identity.Key
                        : null,
                    (RuntimeProfilingDeliveryOutcome)delivery.Outcome,
                    delivery.Detail,
                    delivery.Duration
                ))
                .ToArray()
        );

    private async Task FailCreatedSessionAsync(RuntimeProfilingSession session)
    {
        await store
            .TryTransitionSessionAsync(
                session.Identity.Id,
                [RuntimeProfilingSessionState.Running],
                RuntimeProfilingSessionState.Failed,
                timeProvider.GetUtcNow(),
                CancellationToken.None
            )
            .ConfigureAwait(false);
    }

    private static string NormalizeName(string name, DateTimeOffset now) =>
        string.IsNullOrWhiteSpace(name)
            ? now.ToString(RuntimeProfilingOptions.DefaultSessionNameFormat, CultureInfo.InvariantCulture)
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

    private static IResultError ValidateStartRequest(RuntimeProfilingStartRequest request) =>
        request is null ? new ProfilingValidationError("A profiling start request is required.")
        : request.SamplingInterval is { } interval
        && interval < RuntimeProfilingOptions.MinimumSamplingInterval
            ? new ProfilingValidationError(
                "The profiling sampling interval must be at least 500 ms."
            )
        : request.Duration is { } duration && duration <= TimeSpan.Zero
            ? new ProfilingValidationError("The profiling duration must be positive.")
        : null;

    private static bool IsNoActiveSession(IResult<RuntimeProfilingSession> result) =>
        result.Errors.Any(error => error is ProfilingInvalidStateError);

    private static Result<T> Failure<T>(IResultError error) => Result<T>.Failure().WithError(error);

    private static Result<TTarget> CopyFailure<TTarget, TSource>(IResult<TSource> source) =>
        Result<TTarget>.Failure().WithErrors(source.Errors).WithMessages(source.Messages);

    private sealed record PreparedTargets(
        RuntimeProfilingBroadcastTargetSnapshot Snapshot,
        Dictionary<string, ProfilingNode> Nodes
    );
}
