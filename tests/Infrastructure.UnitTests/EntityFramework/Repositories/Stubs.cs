// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories;

using System;
using Microsoft.EntityFrameworkCore;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<PersonDtoStub> Persons { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("Test");
        modelBuilder.Entity<PersonDtoStub>().ToTable("Persons").HasKey(nameof(PersonDtoStub.Identifier));
    }
}

public sealed class TestDbContextFixture : IDisposable
{
    public TestDbContextFixture()
    {
        var builder = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"Test_{KeyGenerator.Create(10)}");

        this.Context = new TestDbContext(builder.Options);
        this.Context.Persons.Add(new PersonDtoStub() { Identifier = Guid.NewGuid(), FullName = "John Doe" });
        this.Context.Persons.Add(new PersonDtoStub() { Identifier = Guid.NewGuid(), FullName = "Jane Doe" });
        this.Context.SaveChanges();
    }

    public TestDbContext Context { get; }

    public void Dispose()
    {
        this.Context.Dispose();
    }
}
