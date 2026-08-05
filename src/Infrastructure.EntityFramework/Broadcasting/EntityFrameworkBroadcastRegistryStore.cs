// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting;

using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Stores shared Broadcasting node discovery state through an application-owned DbContext.</summary>
/// <typeparam name="TContext">The application DbContext implementing <see cref="IBroadcastingContext"/>.</typeparam>
/// <param name="scopeFactory">Creates operation-owned dependency-injection scopes.</param>
/// <param name="options">The shared Broadcasting options.</param>
/// <param name="timeProvider">The clock used for delivery and lease timestamps.</param>
/// <param name="metrics">The optional metrics service.</param>
/// <param name="logger">The optional structured logger.</param>
/// <example>
/// <code>
/// services.AddBroadcasting()
///     .WithEntityFrameworkRegistry&lt;AppDbContext&gt;();
/// </code>
/// </example>
public sealed class EntityFrameworkBroadcastRegistryStore<TContext>(
    IServiceScopeFactory scopeFactory,
    BroadcastingOptions options,
    TimeProvider timeProvider,
    IMetricsService metrics = null,
    ILogger<EntityFrameworkBroadcastRegistryStore<TContext>> logger = null
) : IBroadcastRegistryStore
    where TContext : DbContext, IBroadcastingContext
{
    /// <inheritdoc />
    public BroadcastRegistryCapabilities Capabilities { get; } = new(true, true);

    /// <inheritdoc />
    public Task UpsertAsync(
        BroadcastNodeRegistrationRequest request,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var normalizedIdentity = Normalize(request.NodeIdentity);
                var entity = await context
                    .BroadcastNodeRegistrations.Include(x => x.Scopes)
                    .SingleOrDefaultAsync(
                        x => x.NormalizedNodeIdentity == normalizedIdentity,
                        token
                    )
                    .ConfigureAwait(false);

                if (entity is null)
                {
                    entity = new BroadcastNodeRegistrationEntity
                    {
                        Id = Guid.NewGuid(),
                        NodeIdentity = request.NodeIdentity,
                        NormalizedNodeIdentity = normalizedIdentity,
                    };
                    context.BroadcastNodeRegistrations.Add(entity);
                }

                entity.NodeIdentity = request.NodeIdentity;
                entity.AdvertisedAddress = request.AdvertisedAddress?.ToString();
                entity.ProcessStartedUtc = request.ProcessStartedUtc;
                entity.RegisteredUtc = request.RegisteredUtc;
                entity.ProtocolVersion = request.ProtocolVersion;
                entity.IsActive = true;
                entity.ConsecutiveFailureCount = 0;
                entity.LastFailureUtc = null;
                entity.LastFailure = null;
                entity.LeaseRenewedUtc = request.LeaseExpiresUtc.HasValue
                    ? request.RegisteredUtc
                    : null;
                entity.LeaseExpiresUtc = request.LeaseExpiresUtc;
                entity.AdvanceConcurrencyVersion();

                var desiredScopes = request
                    .Scopes.Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(Normalize, StringComparer.Ordinal);
                foreach (
                    var scope in entity
                        .Scopes.Where(scope => !desiredScopes.ContainsKey(scope.NormalizedScope))
                        .ToArray()
                )
                {
                    context.BroadcastNodeScopes.Remove(scope);
                    entity.Scopes.Remove(scope);
                }

                foreach (var desiredScope in desiredScopes)
                {
                    var scope = entity.Scopes.SingleOrDefault(existing =>
                        existing.NormalizedScope == desiredScope.Key
                    );
                    if (scope is not null)
                    {
                        scope.Scope = desiredScope.Value;
                        continue;
                    }

                    entity.Scopes.Add(
                        new BroadcastNodeScopeEntity
                        {
                            NodeRegistrationId = entity.Id,
                            NormalizedScope = desiredScope.Key,
                            Scope = desiredScope.Value,
                            NodeRegistration = entity,
                        }
                    );
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
            },
            cancellationToken
        );

    /// <inheritdoc />
    public Task RemoveAsync(string nodeIdentity, CancellationToken cancellationToken = default) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var normalizedIdentity = Normalize(nodeIdentity);
                var entity = await context
                    .BroadcastNodeRegistrations.SingleOrDefaultAsync(
                        x => x.NormalizedNodeIdentity == normalizedIdentity,
                        token
                    )
                    .ConfigureAwait(false);
                if (entity is not null)
                {
                    context.BroadcastNodeRegistrations.Remove(entity);
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                }
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<IReadOnlyList<BroadcastNodeRegistration>> GetActiveAsync(
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default
    )
    {
        var keys = scopes.Select(Normalize).Distinct().ToArray();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var entities = await context
            .BroadcastNodeRegistrations.AsNoTracking()
            .Include(x => x.Scopes)
            .Where(x => x.IsActive && x.Scopes.Any(s => keys.Contains(s.NormalizedScope)))
            .OrderBy(x => x.NormalizedNodeIdentity)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToModel).ToArray();
    }

    /// <inheritdoc />
    public async Task<BroadcastNodeRegistration> FindAsync(
        string nodeIdentity,
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var normalizedIdentity = Normalize(nodeIdentity);
        var entity = await context
            .BroadcastNodeRegistrations.AsNoTracking()
            .Include(x => x.Scopes)
            .SingleOrDefaultAsync(
                x => x.NormalizedNodeIdentity == normalizedIdentity,
                cancellationToken
            )
            .ConfigureAwait(false);
        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc />
    public Task RecordDeliveryAsync(
        string nodeIdentity,
        bool succeeded,
        string failure,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var key = Normalize(nodeIdentity);
                var entity = await context
                    .BroadcastNodeRegistrations.SingleOrDefaultAsync(
                        x => x.NormalizedNodeIdentity == key,
                        token
                    )
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return;
                }

                var now = timeProvider.GetUtcNow();
                if (succeeded)
                {
                    entity.LastSuccessUtc = now;
                    entity.LastFailureUtc = null;
                    entity.LastFailure = null;
                    entity.ConsecutiveFailureCount = 0;
                    entity.IsActive = true;
                }
                else
                {
                    entity.LastFailureUtc = now;
                    entity.LastFailure = failure?.Length > 4000 ? failure[..4000] : failure;
                    entity.ConsecutiveFailureCount++;
                    entity.IsActive =
                        entity.ConsecutiveFailureCount < options.UnreachableFailureThreshold;
                }

                entity.AdvanceConcurrencyVersion();
                await context.SaveChangesAsync(token).ConfigureAwait(false);
            },
            cancellationToken
        );

    /// <inheritdoc />
    public Task RenewLeaseAsync(
        string nodeIdentity,
        DateTimeOffset leaseExpiresUtc,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var key = Normalize(nodeIdentity);
                var entity = await context
                    .BroadcastNodeRegistrations.SingleOrDefaultAsync(
                        x => x.NormalizedNodeIdentity == key,
                        token
                    )
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return;
                }

                entity.IsActive = true;
                entity.LeaseRenewedUtc = timeProvider.GetUtcNow();
                entity.LeaseExpiresUtc = leaseExpiresUtc;
                entity.AdvanceConcurrencyVersion();
                await context.SaveChangesAsync(token).ConfigureAwait(false);
            },
            cancellationToken
        );

    /// <inheritdoc />
    public Task ExpireLeasesAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default
    ) =>
        this.ExecuteWriteAsync(
            async (context, token) =>
            {
                var entities = await context
                    .BroadcastNodeRegistrations.Where(x =>
                        x.IsActive && x.LeaseExpiresUtc != null
                    )
                    .ToListAsync(token)
                    .ConfigureAwait(false);
                var expiredEntities = entities
                    .Where(entity => entity.LeaseExpiresUtc <= utcNow)
                    .ToArray();
                foreach (var entity in expiredEntities)
                {
                    entity.IsActive = false;
                    entity.AdvanceConcurrencyVersion();
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                BroadcastingMetrics.RecordStaleRemoval(metrics, expiredEntities.Length);
                if (expiredEntities.Length > 0 && logger is not null)
                {
                    BroadcastingTypedLogger.LogRegistrationLeasesExpired(
                        logger,
                        "UTL",
                        expiredEntities.Length
                    );
                }
            },
            cancellationToken
        );

    /// <inheritdoc />
    public async Task<IReadOnlyList<BroadcastNodeRegistration>> ListAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var entities = await context
            .BroadcastNodeRegistrations.AsNoTracking()
            .Include(x => x.Scopes)
            .OrderBy(x => x.NormalizedNodeIdentity)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(ToModel).ToArray();
    }

    private static string Normalize(string value) =>
        value?.Trim().ToUpperInvariant() ?? throw new ArgumentNullException(nameof(value));

    private static BroadcastNodeRegistration ToModel(BroadcastNodeRegistrationEntity entity) =>
        new()
        {
            NodeIdentity = entity.NodeIdentity,
            AdvertisedAddress = string.IsNullOrWhiteSpace(entity.AdvertisedAddress)
                ? null
                : new Uri(entity.AdvertisedAddress, UriKind.Absolute),
            Scopes = entity
                .Scopes.Select(x => x.Scope)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ProcessStartedUtc = entity.ProcessStartedUtc,
            RegisteredUtc = entity.RegisteredUtc,
            ProtocolVersion = entity.ProtocolVersion,
            IsActive = entity.IsActive,
            LastSuccessUtc = entity.LastSuccessUtc,
            LastFailureUtc = entity.LastFailureUtc,
            LastFailure = entity.LastFailure,
            ConsecutiveFailureCount = entity.ConsecutiveFailureCount,
            LeaseExpiresUtc = entity.LeaseExpiresUtc,
            LeaseRenewedUtc = entity.LeaseRenewedUtc,
        };

    private async Task ExecuteWriteAsync(
        Func<TContext, CancellationToken, Task> action,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            try
            {
                await action(context, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                // Retry once with a fresh operation-owned context.
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // A competing insert may have won the normalized identity key.
            }
        }
    }
}
