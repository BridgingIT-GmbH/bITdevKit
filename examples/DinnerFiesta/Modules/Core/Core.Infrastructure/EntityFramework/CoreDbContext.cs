// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure;

using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class CoreDbContext : ModuleDbContextBase, IDocumentStoreContext, IOutboxDomainEventContext, IOutboxMessageContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options)
        : base(options)
    {
    }

    // All aggregate roots and entities are exposed as dbsets
    public DbSet<Host> Hosts { get; set; }

    public DbSet<User> Users { get; set; }

    public DbSet<Guest> Guests { get; set; }

    public DbSet<Bill> Bills { get; set; }

    public DbSet<Dinner> Dinners { get; set; }

    public DbSet<Menu> Menus { get; set; }

    public DbSet<MenuReview> MenuReviews { get; set; }

    public DbSet<StorageDocument> StorageDocuments { get; set; }

    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TODO: seed some data here

        base.OnModelCreating(modelBuilder);
    }
}