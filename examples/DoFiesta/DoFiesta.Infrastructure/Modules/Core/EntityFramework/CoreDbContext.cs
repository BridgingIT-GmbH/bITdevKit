// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Infrastructure;

using DevKit.Infrastructure.EntityFramework;
using Domain.Model;
using Microsoft.EntityFrameworkCore;

public class CoreDbContext(DbContextOptions<CoreDbContext> options) :
    ModuleDbContextBase(options),
    IOutboxDomainEventContext,
    IEntityPermissionContext,
    IFileMonitoringContext,
    ILoggingContext
{
    public DbSet<TodoItem> TodoItems { get; set; }

    public DbSet<Subscription> Subscriptions { get; set; }

    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    public DbSet<EntityPermission> EntityPermissions { get; set; }

    public DbSet<FileEventEntity> FileEvents { get; set; }

    public DbSet<LogEntry> LogEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>("TodoItemSequence")
            .StartsAt(1).IncrementsBy(1);

        base.OnModelCreating(modelBuilder);
    }
}