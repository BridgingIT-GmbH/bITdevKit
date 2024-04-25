// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Infrastructure;

using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class CustomerTypeConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(
                id => id.Value,
                value => CustomerId.Create(value));

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(128);

        builder.OwnsOne(b => b.Email, pb =>
        {
            pb.HasIndex(nameof(Customer.Email.Value))
              .IsUnique(true);
            pb.Property(e => e.Value)
              .IsRequired()
              .HasMaxLength(256);
        });

        builder.OwnsOneAuditState();
    }
}