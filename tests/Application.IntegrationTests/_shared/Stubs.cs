// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests;

using Domain.Model;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class StubDbContext : DbContext, IOutboxDomainEventContext, IOutboxMessageContext, IDocumentStoreContext
{
    public StubDbContext() { }

    public StubDbContext(DbContextOptions options)
        : base(options) { }

    public DbSet<PersonStub> Persons { get; set; }

    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public DbSet<StorageDocument> StorageDocuments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("-");
        }
    }
}

public class PersonStub : AggregateRoot<Guid>
{
    public string Country { get; set; } = "USA";

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }
}