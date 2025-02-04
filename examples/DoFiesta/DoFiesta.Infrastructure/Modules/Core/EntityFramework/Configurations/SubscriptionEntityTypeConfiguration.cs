// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Infrastructure;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class SubscriptionEntityTypeConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.ConcurrencyVersion).IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => SubscriptionId.Create(value));

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Plan)
            .HasConversion(new EnumerationConverter<SubscriptionPlan>())
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(new EnumerationConverter<SubscriptionStatus>())
            .IsRequired();

        builder.Property(x => x.BillingCycle)
            .HasConversion(new EnumerationConverter<SubscriptionBillingCycle>())
            .IsRequired();

        builder.Property(x => x.StartDate).IsRequired();
            //.HasColumnType("datetimeoffset");

        builder.Property(x => x.EndDate);
            //.HasColumnType("datetimeoffset");

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);

        builder.OwnsOneAuditState(); // TODO: use ToJson variant
        //builder.OwnsOne(e => e.AuditState, b => b.ToJson());
    }
}