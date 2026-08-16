// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using Common;
using Domain.EventSourcing.Registration;
using Infrastructure.EventSourcing;
using Models;
using Repositories;

/// <summary>
/// Represents snapshot repository.
/// </summary>
/// <param name="aggregateRegistration">The aggregate registration used by the operation.</param>
/// <param name="options">The options controlling the operation.</param>
public class SnapshotRepository(
    IEventStoreAggregateRegistration aggregateRegistration,
    EntityFrameworkRepositoryOptions options)
    : EntityFrameworkGenericRepository<EventStoreSnapshot /*, EventStoreSnapshotForDatabase*/>(options),
        ISnapshotRepository
{
    private IEventStoreAggregateRegistration aggregateRegistration = aggregateRegistration;
    private EventStoreDbContext context = options.DbContext as EventStoreDbContext;

    /// <summary>
    /// Initializes a new instance of the <c>SnapshotRepository</c> class.
    /// </summary>
    /// <param name="aggregateRegistration">The aggregate registration used by the operation.</param>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    public SnapshotRepository(
        IEventStoreAggregateRegistration aggregateRegistration,
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(aggregateRegistration, optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    /// <summary>
    /// Gets snapshot.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="immutableName">The immutable name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<byte[]> GetSnapshotAsync(
        Guid aggregateId,
        string immutableName,
        CancellationToken cancellationToken)
    {
        var snapshots = await this.ProjectAllAsync(new AggregateSnapshotSpecification(aggregateId, immutableName),
                p => p.Data,
                null,
                cancellationToken)
            .AnyContext();

        return snapshots.ToArray().FirstOrDefault();
    }

    /// <summary>
    /// Saves snapshot.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="blob">The blob used by the operation.</param>
    /// <param name="immutableName">The immutable name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SaveSnapshotAsync(
        Guid aggregateId,
        byte[] blob,
        string immutableName,
        CancellationToken cancellationToken)
    {
        var snapshot = await this.FindOneAsync(new AggregateSnapshotSpecification(aggregateId, immutableName),
                cancellationToken: cancellationToken)
            .AnyContext();
        if (snapshot is null)
        {
            snapshot = new EventStoreSnapshot { Id = aggregateId, AggregateType = immutableName };
        }

        snapshot.Data = blob;
        snapshot.SnapshotDate = DateTime.Now;
        await this.UpsertAsync(snapshot, cancellationToken).AnyContext();
    }
}
