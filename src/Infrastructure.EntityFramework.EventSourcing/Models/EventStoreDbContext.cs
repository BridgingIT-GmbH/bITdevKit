// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.Models;

using Infrastructure.EventSourcing;
using Microsoft.EntityFrameworkCore;
using Outbox.Models;

public class EventStoreDbContext : DbContext
{
    private readonly IEventStoreConfiguration eventStoreConfiguration;

    public EventStoreDbContext(
        DbContextOptions<EventStoreDbContext> options,
        IEventStoreConfiguration eventStoreConfiguration)
        : base(options)
    {
        this.eventStoreConfiguration = eventStoreConfiguration;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="EventStoreDbContext" /> class.
    /// </summary>
    /// <remarks>Dieser Konstruktor wird von Ableitungen aufgerufen werden</remarks>
    protected EventStoreDbContext(DbContextOptions options, IEventStoreConfiguration eventStoreConfiguration)
        : base(options)
    {
        this.eventStoreConfiguration = eventStoreConfiguration;
    }

    public DbSet<EventStoreAggregateEvent> AggregateEvent { get; set; }

    public DbSet<EventStoreSnapshot> Snapshot { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(this.eventStoreConfiguration.DefaultSchema);
        modelBuilder.Entity<EventStoreAggregateEvent>()
            .ToTable("AggregateEvent")
            .HasIndex(ae => new { ae.AggregateId, ae.AggregateVersion })
            .HasDatabaseName("IX_AggregateEvent_AggregateIDVersion");
        modelBuilder.Entity<EventStoreAggregateEvent>()
            .HasIndex(a => a.AggregateType)
            .HasDatabaseName("IX_AggregateEvent_AggregateType");
        modelBuilder.Entity<EventStoreAggregateEvent>()
            .HasIndex(a => a.AggregateId)
            .HasDatabaseName("IX_AggregateEvent_AggregateId");
        modelBuilder.Entity<EventStoreAggregateEvent>()
            .HasIndex(a => new { a.AggregateId, a.AggregateType, a.AggregateVersion })
            .HasDatabaseName("IX_AggregateEvent_IX_AggregateEvent_AggregateIDAggrTypeVers")
            .IsUnique();
        modelBuilder.Entity<Outbox>()
            .ToTable("EventstoreOutbox")
            .HasIndex(e => e.TimeStamp)
            .HasDatabaseName("IX_EventstoreOutbox_TimeStamp");
        modelBuilder.Entity<EventStoreSnapshot>()
            .ToTable("Snapshot")
            .HasIndex(s => new { s.Id, s.AggregateType })
            .IsUnique();
    }
}