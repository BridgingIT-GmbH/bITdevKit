// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Infrastructure.Repositories;

using DevKit.Infrastructure.EntityFramework.EventSourcing.Models;
using Microsoft.EntityFrameworkCore;
using Models;

public class EventSourcingDemoDbContext(DbContextOptions<EventSourcingDemoDbContext> options) : EventStoreDbContext(options, new EventStoreConfiguration() { DefaultSchema = "EventStore" })
{
    public DbSet<PersonDatabaseEntity> Person { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        EnsureArg.IsNotNull(modelBuilder, nameof(modelBuilder));
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.Entity<PersonDatabaseEntity>().ToTable("Person");
    }
}