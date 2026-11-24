// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>Persists node registrations, delivery observations, and leases within one local database owner.</summary>
/// <param name="database">The owned native registry namespace.</param>
/// <param name="options">The shared Broadcasting options.</param>
/// <param name="timeProvider">The clock used for delivery and lease timestamps.</param>
/// <param name="metrics">The optional metrics service.</param>
/// <param name="logger">The optional structured logger.</param>
/// <example>
/// <code>
/// services.AddBroadcasting().WithLiteDbRegistry();
/// </code>
/// </example>
public sealed class LiteDbBroadcastRegistryStore(
    LiteDbFeatureDatabase database,
    BroadcastingOptions options,
    TimeProvider timeProvider,
    IMetricsService metrics = null,
    ILogger<LiteDbBroadcastRegistryStore> logger = null
) : IBroadcastRegistryStore
{
    private readonly LiteDbKeyedCollection<string, BroadcastNodeRegistration> registrations = database.Collection<string, BroadcastNodeRegistration>("nodes", ignoreCase: true);

    /// <inheritdoc />
    public BroadcastRegistryCapabilities Capabilities { get; } = new(false, false);

    /// <inheritdoc />
    public Task UpsertAsync(
        BroadcastNodeRegistrationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return database.ExecuteAsync(() =>
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            this.registrations.TryGetValue(request.NodeIdentity, out var existing);
            this.registrations[request.NodeIdentity] = new BroadcastNodeRegistration
            {
                NodeIdentity = request.NodeIdentity,
                AdvertisedAddress = request.AdvertisedAddress,
                Scopes = NormalizeScopes(request.Scopes),
                ProcessStartedUtc = request.ProcessStartedUtc,
                RegisteredUtc = request.RegisteredUtc,
                ProtocolVersion = request.ProtocolVersion,
                IsActive = true,
                LastSuccessUtc = existing?.LastSuccessUtc,
                LastFailureUtc = null,
                LastFailure = null,
                ConsecutiveFailureCount = 0,
                LeaseExpiresUtc = request.LeaseExpiresUtc,
                LeaseRenewedUtc = request.LeaseExpiresUtc.HasValue ? request.RegisteredUtc : null,
            };

            return Task.CompletedTask;

        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string nodeIdentity, CancellationToken cancellationToken = default)
    {
        return database.ExecuteAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.registrations.Remove(nodeIdentity);

            return Task.CompletedTask;

        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BroadcastNodeRegistration>> GetActiveAsync(
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default
    )
    {
        return database.ExecuteAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedScopes = new HashSet<string>(
                NormalizeScopes(scopes),
                StringComparer.OrdinalIgnoreCase
            );

            return Task.FromResult<IReadOnlyList<BroadcastNodeRegistration>>(
                this.registrations.Values.Where(x =>
                        x.IsActive && (!x.LeaseExpiresUtc.HasValue || x.LeaseExpiresUtc > timeProvider.GetUtcNow()) && x.Scopes.Any(normalizedScopes.Contains)
                    )
                    .OrderBy(x => x.NodeIdentity, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            );

        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<BroadcastNodeRegistration> FindAsync(
        string nodeIdentity,
        CancellationToken cancellationToken = default
    )
    {
        return database.ExecuteAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.registrations.TryGetValue(nodeIdentity, out var registration);
            return Task.FromResult(registration);

        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordDeliveryAsync(
        string nodeIdentity,
        bool succeeded,
        string failure,
        CancellationToken cancellationToken = default
    )
    {
        return database.ExecuteAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!this.registrations.TryGetValue(nodeIdentity, out var registration))
            {
                return Task.CompletedTask;
            }

            var now = timeProvider.GetUtcNow();
            var failures = succeeded ? 0 : registration.ConsecutiveFailureCount + 1;
            this.registrations[nodeIdentity] = registration with
            {
                IsActive = succeeded || failures < options.UnreachableFailureThreshold,
                LastSuccessUtc = succeeded ? now : registration.LastSuccessUtc,
                LastFailureUtc = succeeded ? null : now,
                LastFailure =
                    succeeded ? null
                    : failure?.Length > 4000 ? failure[..4000]
                    : failure,
                ConsecutiveFailureCount = failures,
            };

            return Task.CompletedTask;

        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task RenewLeaseAsync(
        string nodeIdentity,
        DateTimeOffset leaseExpiresUtc,
        CancellationToken cancellationToken = default
    )
    {
        return database.ExecuteAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.registrations.TryGetValue(nodeIdentity, out var registration))
            {
                this.registrations[nodeIdentity] = registration with
                {
                    IsActive = true,
                    LeaseExpiresUtc = leaseExpiresUtc,
                    LeaseRenewedUtc = timeProvider.GetUtcNow(),
                };
            }

            return Task.CompletedTask;

        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task ExpireLeasesAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default
    )
    {
        return database.ExecuteAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expiredCount = 0;
            foreach (var pair in this.registrations.ToArray())
            {
                if (
                    pair.Value.IsActive
                    && pair.Value.LeaseExpiresUtc is { } expiry
                    && expiry <= utcNow
                )
                {
                    this.registrations[pair.Key] = pair.Value with { IsActive = false };
                    expiredCount++;
                }
            }

            BroadcastingMetrics.RecordStaleRemoval(metrics, expiredCount);
            if (expiredCount > 0 && logger is not null)
            {
                BroadcastingTypedLogger.LogRegistrationLeasesExpired(logger, "UTL", expiredCount);
            }

            return Task.CompletedTask;

        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BroadcastNodeRegistration>> ListAsync(
        CancellationToken cancellationToken = default
    )
    {
        return database.ExecuteAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<BroadcastNodeRegistration>>(
                this.registrations.Values.OrderBy(
                        x => x.NodeIdentity,
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ToArray()
            );

        }, cancellationToken);
    }

    private static IReadOnlyCollection<string> NormalizeScopes(IEnumerable<string> scopes) =>
        (scopes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
