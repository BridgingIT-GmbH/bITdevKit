// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DinnerEntityTypeConfiguration : IEntityTypeConfiguration<Dinner>
{
    public void Configure(EntityTypeBuilder<Dinner> builder)
    {
        ConfigureDinnersTable(builder);
        ConfigureDinnerReservationsTable(builder);
    }

    private static void ConfigureDinnerReservationsTable(EntityTypeBuilder<Dinner> builder)
    {
        builder.OwnsMany(d => d.Reservations, rb =>
        {
            rb.ToTable("DinnerReservations");

            rb.WithOwner().HasForeignKey("DinnerId");

            rb.HasKey("DinnerId", "Id");

            rb.Property(r => r.Id)
                .ValueGeneratedNever()
                .HasConversion(
                    id => id.Value,
                    value => DinnerReservationId.Create(value));

            rb.Property(r => r.GuestId)
                .HasConversion(
                    id => id.Value,
                    value => GuestId.Create(value));

            rb.Property(r => r.BillId)
                .HasConversion(
                    id => id.Value,
                    value => BillId.Create(value));

            rb.Property(r => r.Status)
                .HasConversion(
                    status => status.Id,
                    value => Enumeration.From<DinnerReservationStatus>(value));

            rb.OwnsOneAuditState();
        });

        builder.Metadata.FindNavigation(nameof(Dinner.Reservations))
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureDinnersTable(EntityTypeBuilder<Dinner> builder)
    {
        builder.ToTable("Dinners");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(
                id => id.Value,
                value => DinnerId.Create(value));

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(d => d.Description)
            .IsRequired()
            .HasMaxLength(512);

        builder.OwnsOne(d => d.Schedule, s =>
        {
            s.Property(d => d.StartDateTime)
                .IsRequired();

            s.Property(d => d.EndDateTime)
                .IsRequired();
        });

        builder.Property(d => d.Status)
            .HasConversion(
                status => status.Id,
                value => Enumeration.From<DinnerStatus>(value));

        builder.OwnsOne(d => d.Price, pb =>
            pb.Property(p => p.Amount)
                .HasColumnType("decimal(5,2)"));

        builder.Property(d => d.HostId)
            .HasConversion(
                id => id.Value,
                value => HostId.Create(value));

        builder.Property(d => d.MenuId)
            .HasConversion(
                id => id.Value,
                value => MenuId.Create(value));

        builder.OwnsOne(d => d.Location, l =>
        {
            l.Property(e => e.AddressLine1)
             .HasMaxLength(256);

            l.Property(e => e.AddressLine2)
             .HasMaxLength(256);

            l.Property(e => e.PostalCode)
             .HasMaxLength(16);

            l.Property(e => e.City)
             .HasMaxLength(128);

            l.Property(e => e.Country)
             .HasMaxLength(128);

            l.Property(e => e.Latitude)
             .HasColumnType("decimal(10,7)")
             .IsRequired(false);

            l.Property(e => e.Longitude)
             .HasColumnType("decimal(10,7)")
             .IsRequired(false);
        });

        builder.OwnsOneAuditState();
    }
}