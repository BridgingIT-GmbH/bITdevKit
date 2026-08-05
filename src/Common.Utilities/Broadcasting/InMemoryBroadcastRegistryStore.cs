// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>Stores process-local node registrations for development and tests.</summary>
/// <param name="options">The shared Broadcasting options.</param>
/// <param name="timeProvider">The clock used for delivery and lease timestamps.</param>
/// <param name="metrics">The optional metrics service.</param>
/// <param name="logger">The optional structured logger.</param>
/// <example>
/// <code>
/// var store = new InMemoryBroadcastRegistryStore(options, TimeProvider.System);
/// </code>
/// </example>
public sealed class InMemoryBroadcastRegistryStore(
    BroadcastingOptions options,
    TimeProvider timeProvider,
    IMetricsService metrics = null,
    ILogger<InMemoryBroadcastRegistryStore> logger = null
) : IBroadcastRegistryStore
{
    private readonly object sync = new();
    private readonly Dictionary<string, BroadcastNodeRegistration> registrations = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly BroadcastingOptions options = options;
    private readonly TimeProvider timeProvider = timeProvider;

    /// <inheritdoc />
    public BroadcastRegistryCapabilities Capabilities { get; } = new(false, false);

    /// <inheritdoc />
    public Task UpsertAsync(
        BroadcastNodeRegistrationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
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
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string nodeIdentity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            this.registrations.Remove(nodeIdentity);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BroadcastNodeRegistration>> GetActiveAsync(
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedScopes = new HashSet<string>(
            NormalizeScopes(scopes),
            StringComparer.OrdinalIgnoreCase
        );

        lock (this.sync)
        {
            return Task.FromResult<IReadOnlyList<BroadcastNodeRegistration>>(
                this.registrations.Values.Where(x =>
                        x.IsActive && x.Scopes.Any(normalizedScopes.Contains)
                    )
                    .OrderBy(x => x.NodeIdentity, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            );
        }
    }

    /// <inheritdoc />
    public Task<BroadcastNodeRegistration> FindAsync(
        string nodeIdentity,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            this.registrations.TryGetValue(nodeIdentity, out var registration);
            return Task.FromResult(registration);
        }
    }

    /// <inheritdoc />
    public Task RecordDeliveryAsync(
        string nodeIdentity,
        bool succeeded,
        string failure,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            if (!this.registrations.TryGetValue(nodeIdentity, out var registration))
            {
                return Task.CompletedTask;
            }

            var now = this.timeProvider.GetUtcNow();
            var failures = succeeded ? 0 : registration.ConsecutiveFailureCount + 1;
            this.registrations[nodeIdentity] = registration with
            {
                IsActive = succeeded || failures < this.options.UnreachableFailureThreshold,
                LastSuccessUtc = succeeded ? now : registration.LastSuccessUtc,
                LastFailureUtc = succeeded ? null : now,
                LastFailure =
                    succeeded ? null
                    : failure?.Length > 4000 ? failure[..4000]
                    : failure,
                ConsecutiveFailureCount = failures,
            };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RenewLeaseAsync(
        string nodeIdentity,
        DateTimeOffset leaseExpiresUtc,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            if (this.registrations.TryGetValue(nodeIdentity, out var registration))
            {
                this.registrations[nodeIdentity] = registration with
                {
                    IsActive = true,
                    LeaseExpiresUtc = leaseExpiresUtc,
                    LeaseRenewedUtc = this.timeProvider.GetUtcNow(),
                };
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ExpireLeasesAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expiredCount = 0;
        lock (this.sync)
        {
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
        }

        BroadcastingMetrics.RecordStaleRemoval(metrics, expiredCount);
        if (expiredCount > 0 && logger is not null)
        {
            BroadcastingTypedLogger.LogRegistrationLeasesExpired(logger, "UTL", expiredCount);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BroadcastNodeRegistration>> ListAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            return Task.FromResult<IReadOnlyList<BroadcastNodeRegistration>>(
                this.registrations.Values.OrderBy(
                        x => x.NodeIdentity,
                        StringComparer.OrdinalIgnoreCase
                    )
                    .ToArray()
            );
        }
    }

    private static IReadOnlyCollection<string> NormalizeScopes(IEnumerable<string> scopes) =>
        (scopes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
