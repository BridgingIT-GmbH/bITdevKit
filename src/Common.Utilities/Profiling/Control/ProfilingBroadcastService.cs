// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
/// Adapts the standalone Broadcast service to Profiling's fixed participant snapshot semantics.
/// </summary>
/// <remarks>
/// Target preparation belongs to Profiling because it must validate provider capability before
/// mutating session state. Actual serialization and delivery are delegated to an unchanged
/// <see cref="BroadcastService"/>.
/// </remarks>
/// <example><code>var targets = await service.PrepareTargetsAsync(cancellationToken: token);</code></example>
public sealed class ProfilingBroadcastService(
    BroadcastingOptions options,
    IBroadcastNodeIdentityProvider identityProvider,
    IBroadcastRegistryStore registry,
    IBroadcastReceiver receiver,
    IBroadcastTransport transport,
    ISerializer serializer,
    TimeProvider timeProvider,
    IMetricsService metrics = null,
    ILogger<BroadcastService> broadcastLogger = null,
    ILogger<ProfilingBroadcastService> logger = null
) : IProfilingBroadcastService
{
    /// <inheritdoc />
    public async Task<Result<ProfilingBroadcastTargetSnapshot>> PrepareTargetsAsync(
        IEnumerable<string> targetScopes = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!options.Enabled)
        {
            return Failure<ProfilingBroadcastTargetSnapshot>(new BroadcastingDisabledError());
        }

        options.Validate();
        var scopes = NormalizeScopes(targetScopes);
        var configuredScopes = new HashSet<string>(
            options.Scopes,
            StringComparer.OrdinalIgnoreCase
        );
        var forbiddenScope = scopes.FirstOrDefault(scope => !configuredScopes.Contains(scope));
        if (forbiddenScope is not null)
        {
            return Failure<ProfilingBroadcastTargetSnapshot>(
                new BroadcastScopeForbiddenError(
                    $"The target scope '{forbiddenScope}' is not configured for this host."
                )
            );
        }

        var senderIdentity = identityProvider.GetNodeIdentity();
        try
        {
            if (registry.Capabilities.IsShared)
            {
                var sender = await registry
                    .FindAsync(senderIdentity, cancellationToken)
                    .ConfigureAwait(false);
                if (sender is null || !sender.IsActive)
                {
                    return Failure<ProfilingBroadcastTargetSnapshot>(
                        new BroadcastSenderNotRegisteredError(
                            "The publishing node is not active in the shared broadcast registry."
                        )
                    );
                }

                var senderScopes = new HashSet<string>(
                    sender.Scopes,
                    StringComparer.OrdinalIgnoreCase
                );
                forbiddenScope = scopes.FirstOrDefault(scope => !senderScopes.Contains(scope));
                if (forbiddenScope is not null)
                {
                    return Failure<ProfilingBroadcastTargetSnapshot>(
                        new BroadcastScopeForbiddenError(
                            $"The target scope '{forbiddenScope}' is not present in the sender registration."
                        )
                    );
                }
            }

            var targets = await registry
                .GetActiveAsync(scopes, cancellationToken)
                .ConfigureAwait(false);
            return Result<ProfilingBroadcastTargetSnapshot>.Success(
                new(scopes, targets, senderIdentity)
            );
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger?.LogWarning(
                "[{LogKey}] profiling Broadcast target preparation failed (exceptionType={ExceptionType})",
                "UTL",
                exception.GetType().Name
            );
            return Failure<ProfilingBroadcastTargetSnapshot>(
                new BroadcastRegistryUnavailableError("The broadcast registry is unavailable.")
            );
        }
    }

    /// <inheritdoc />
    public Task<Result<BroadcastResult>> PublishAsync<TBroadcast>(
        TBroadcast payload,
        ProfilingBroadcastTargetSnapshot targetSnapshot,
        BroadcastPublishOptions publishOptions = null,
        CancellationToken cancellationToken = default
    )
        where TBroadcast : IProfilingBroadcast
    {
        if (targetSnapshot is null)
        {
            return Task.FromResult(
                Failure<BroadcastResult>(
                    new BroadcastValidationError(
                        "A prepared Profiling target snapshot is required."
                    )
                )
            );
        }

        if (
            !string.Equals(
                targetSnapshot.SenderNodeIdentity,
                identityProvider.GetNodeIdentity(),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return Task.FromResult(
                Failure<BroadcastResult>(
                    new BroadcastValidationError(
                        "The prepared Profiling target snapshot belongs to another publishing node."
                    )
                )
            );
        }

        var snapshotRegistry = new ProfilingSnapshotBroadcastRegistry(targetSnapshot, registry);
        var broadcastService = new BroadcastService(
            options,
            identityProvider,
            snapshotRegistry,
            receiver,
            transport,
            serializer,
            timeProvider,
            metrics,
            broadcastLogger
        );
        return broadcastService.PublishAsync(
            payload,
            targetSnapshot.TargetScopes,
            publishOptions,
            cancellationToken
        );
    }

    private static string[] NormalizeScopes(IEnumerable<string> targetScopes)
    {
        var scopes = (targetScopes ?? [])
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return scopes.Length == 0 ? [BroadcastingOptions.DefaultScope] : scopes;
    }

    private static Result<T> Failure<T>(IResultError error) => Result<T>.Failure().WithError(error);

    private sealed class ProfilingSnapshotBroadcastRegistry(
        ProfilingBroadcastTargetSnapshot snapshot,
        IBroadcastRegistryStore registry
    ) : IBroadcastRegistryStore
    {
        public BroadcastRegistryCapabilities Capabilities { get; } = new(false, false);

        public Task UpsertAsync(
            BroadcastNodeRegistrationRequest request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task RemoveAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IReadOnlyList<BroadcastNodeRegistration>> GetActiveAsync(
            IReadOnlyCollection<string> scopes,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot.Targets);
        }

        public Task<BroadcastNodeRegistration> FindAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task RecordDeliveryAsync(
            string nodeIdentity,
            bool succeeded,
            string failure,
            CancellationToken cancellationToken = default
        ) => registry.RecordDeliveryAsync(nodeIdentity, succeeded, failure, cancellationToken);

        public Task RenewLeaseAsync(
            string nodeIdentity,
            DateTimeOffset leaseExpiresUtc,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task ExpireLeasesAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<IReadOnlyList<BroadcastNodeRegistration>> ListAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
