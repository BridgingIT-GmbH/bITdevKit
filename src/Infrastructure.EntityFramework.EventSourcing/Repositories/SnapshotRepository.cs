// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.Models;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using BridgingIT.DevKit.Infrastructure.EventSourcing;

public class SnapshotRepository :
    EntityFrameworkGenericRepository<EventStoreSnapshot/*, EventStoreSnapshotForDatabase*/>,
    ISnapshotRepository
{
    private IEventStoreAggregateRegistration aggregateRegistration;
    private EventStoreDbContext context;

    public SnapshotRepository(IEventStoreAggregateRegistration aggregateRegistration,
        EntityFrameworkRepositoryOptions options)
        : base(options)
    {
        this.aggregateRegistration = aggregateRegistration;
        this.context = options.DbContext as EventStoreDbContext;
    }

    public SnapshotRepository(IEventStoreAggregateRegistration aggregateRegistration,
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(aggregateRegistration, optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build())
    {
    }

    public async Task<byte[]> GetSnapshotAsync(Guid aggregateId, string immutableName, CancellationToken cancellationToken)
    {
        var snapshots = await this
            .ProjectAllAsync(new AggregateSnapshotSpecification(aggregateId, immutableName),
                p => p.Data, null, cancellationToken: cancellationToken).AnyContext();
        return snapshots.ToArray().FirstOrDefault();
    }

    public async Task SaveSnapshotAsync(Guid aggregateId, byte[] blob, string immutableName, CancellationToken cancellationToken)
    {
        var snapshot = await this.FindOneAsync(new AggregateSnapshotSpecification(aggregateId, immutableName), cancellationToken: cancellationToken)
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