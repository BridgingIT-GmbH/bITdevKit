// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

using System.Linq.Expressions;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

public class PersonStub : AggregateRoot<Guid>
{
    private readonly List<LocationStub> locations = new();

    public PersonStub()
    {
    }

    public PersonStub(string firstName, string lastName, string email, int age)
    {
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = EmailAddressStub.Create(email);
        this.Age = age;
    }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Nationality { get; set; } = "USA";

    public EmailAddressStub Email { get; set; }

    public IReadOnlyList<LocationStub> Locations => this.locations.AsReadOnly();

    public int Age { get; set; }

    public PersonStub AddLocation(LocationStub location)
    {
        this.locations.Add(location);
        return this;
    }

    public PersonStub RemoveLocation(LocationStub location)
    {
        if (this.locations.Contains(location))
        {
            this.locations.Remove(location);
        }

        return this;
    }
}

public class EmailAddressStub : ValueObject
{
    private EmailAddressStub()
    {
    }

    private EmailAddressStub(string value)
    {
        this.Value = value;
    }

    public string Value { get; private set; }

    public static implicit operator string(EmailAddressStub email) => email.Value;

    public static EmailAddressStub Create(string value)
    {
        value = value?.Trim()?.ToLowerInvariant();

        return new EmailAddressStub(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}

public class LocationStub : Entity<Guid>
{
    private LocationStub()
    {
    }

    private LocationStub(
        string name,
        string addressLine1,
        string addressLine2,
        string postalCode,
        string city,
        string country)
    {
        this.Name = name;
        this.AddressLine1 = addressLine1;
        this.AddressLine2 = addressLine2;
        this.PostalCode = postalCode;
        this.City = city;
        this.Country = country;
    }

    public string Name { get; private set; }

    public string AddressLine1 { get; private set; }

    public string AddressLine2 { get; private set; }

    public string PostalCode { get; private set; }

    public string City { get; private set; }

    public string Country { get; private set; }

    public static LocationStub Create(
        string name,
        string addressLine1,
        string addressLine2,
        string postalCode,
        string city,
        string country)
    {
        return new LocationStub(
            name,
            addressLine1,
            addressLine2,
            postalCode,
            city,
            country);
    }
}

public class PersonByEmailSpecification(string email) : Specification<PersonStub>
{
    private readonly string email = email;

    public override Expression<Func<PersonStub, bool>> ToExpression()
    {
        return t => t.Email.Value == this.email;
    }
}

public class PersonIsAdultSpecification : Specification<PersonStub>
{
    public override Expression<Func<PersonStub, bool>> ToExpression()
    {
        return t => t.Age >= 18;
    }
}

public class PersonDomainEventStub(long ticks) : DomainEventBase
{
    public long Ticks { get; } = ticks;
}

public class MessageStub : MessageBase
{
    public string Country { get; set; } = "USA";

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }
}

public class PersonStubDocument
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Nationality { get; set; } = "USA";

    public int Age { get; set; }
}

public class StubDbContext : DbContext, IOutboxDomainEventContext, IOutboxMessageContext, IDocumentStoreContext
{
    public StubDbContext()
    {
    }

    public StubDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<PersonStub> Persons { get; set; }

    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public DbSet<StorageDocument> StorageDocuments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("-"); // needed to create the migrations
            //optionsBuilder.UseSqlite("-"); // needed to create the migrations
            optionsBuilder.ConfigureWarnings(c => c.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(this.GetType().Assembly);
    }
}

public class OutboxDomainEventTypeConfiguration : IEntityTypeConfiguration<OutboxDomainEvent> // needed for cosmos based dbcontext
{
    public void Configure(EntityTypeBuilder<OutboxDomainEvent> builder)
    {
        //builder.ToTable("__Outbox_DomainEvents");
        //builder.HasManualThroughput(600); // cosmos
        //builder.HasAutoscaleThroughput(1000); // cosmos

        builder.Ignore(u => u.RowVersion); // needs to be ignored as the provider cannot handle the string/byte[] casting
        //builder.Property(u => u.RowVersion)
        //    .IsETagConcurrency()
        //    .HasConversion(new BytesToStringConverter())
        //    .Metadata.RemoveAnnotation("Relational:Timestamp");
    }
}

public class OutboxMessageTypeConfiguration : IEntityTypeConfiguration<OutboxMessage> // needed for cosmos based dbcontext
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        //builder.ToTable("__Outbox_Messages");
        //builder.HasManualThroughput(600); // cosmos
        //builder.HasAutoscaleThroughput(1000); // cosmos

        builder.Ignore(u => u.RowVersion);
        //builder.Property(u => u.RowVersion)
        //    .IsETagConcurrency()
        //    .HasConversion(new BytesToStringConverter())
        //    .Metadata.RemoveAnnotation("Relational:Timestamp");
    }
}

public class StorageDocumentTypeConfiguration : IEntityTypeConfiguration<StorageDocument> // needed for cosmos based dbcontext
{
    public void Configure(EntityTypeBuilder<StorageDocument> builder)
    {
        //builder.ToTable("__Storage_Documents");
        //builder.HasManualThroughput(600); // cosmos
        //builder.HasAutoscaleThroughput(1000); // cosmos

        builder.Ignore(u => u.RowVersion);
        //builder.Property(u => u.RowVersion)
        //    .IsETagConcurrency()
        //    .HasConversion(new BytesToStringConverter())
        //    .Metadata.RemoveAnnotation("Relational:Timestamp");
    }
}

public class PersonStubEntityTypeConfiguration : IEntityTypeConfiguration<PersonStub>
{
    public void Configure(EntityTypeBuilder<PersonStub> builder)
    {
        //builder.HasManualThroughput(600); // cosmos
        //builder.HasAutoscaleThroughput(1000); // cosmos

        builder.ToTable("Persons");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName)
            .IsRequired().HasMaxLength(128);

        builder.Property(u => u.LastName)
            .IsRequired().HasMaxLength(128);

        builder.Property(e => e.Nationality)
                .IsRequired().HasMaxLength(128);

        builder.OwnsOne(b => b.Email, pb =>
        {
            pb.Property(e => e.Value)
              .IsRequired().HasMaxLength(256);
        });

        //builder.HasMany(e => e.Locations)
        //   .WithOne()
        //   .HasForeignKey("PersonId")
        //   .IsRequired()
        //   .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(e => e.Locations, l =>
        {
            l.ToTable("Locations");

            l.HasKey(e => e.Id);

            l.Property(e => e.Name)
                .HasMaxLength(128);

            l.Property(e => e.AddressLine1)
                .IsRequired().HasMaxLength(256);

            l.Property(e => e.AddressLine2)
                .HasMaxLength(256);

            l.Property(e => e.PostalCode)
                .IsRequired().HasMaxLength(16);

            l.Property(e => e.City)
                .IsRequired().HasMaxLength(128);

            l.Property(e => e.Country)
                .IsRequired().HasMaxLength(128);
        });
    }
}

//public class LocationStubEntityTypeConfiguration : IEntityTypeConfiguration<LocationStub>
//{
//    public void Configure(EntityTypeBuilder<LocationStub> builder)
//    {
//        builder.ToTable("Locations");

//        builder.HasKey(u => u.Id);

//        builder.Property(e => e.AddressLine1)
//             .HasMaxLength(256);

//        builder.Property(e => e.AddressLine2)
//             .HasMaxLength(256);

//        builder.Property(e => e.PostalCode)
//             .HasMaxLength(16);

//        builder.Property(e => e.City)
//             .HasMaxLength(128);

//        builder.Property(e => e.Country)
//             .HasMaxLength(128);
//    }
//}