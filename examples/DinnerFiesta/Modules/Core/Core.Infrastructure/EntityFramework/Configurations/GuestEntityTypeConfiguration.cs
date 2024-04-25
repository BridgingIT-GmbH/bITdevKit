// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure;

using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class GuestEntityTypeConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        ConfigureGuestsTable(builder);
        ConfigureGuestUpcomingDinnerIdsTable(builder);
        ConfigureGuestPastDinnerIdsTable(builder);
        ConfigureGuestPendingDinnerIdsTable(builder);
        ConfigureGuestBillIdsTable(builder);
        ConfigureGuestMenuReviewIdsTable(builder);
        ConfigureGuestRatingsTable(builder);
    }

    private static void ConfigureGuestRatingsTable(EntityTypeBuilder<Guest> builder)
    {
        builder.OwnsMany(d => d.Ratings, rb =>
        {
            rb.ToTable("GuestRatings");

            rb.WithOwner().HasForeignKey("GuestId");

            rb.HasKey("Id", "GuestId");

            rb.Property(r => r.Id)
                //.HasColumnName("GuestRatingId")
                .ValueGeneratedNever()
                .HasConversion(
                    id => id.Value,
                    value => GuestRatingId.Create(value));

            rb.Property(r => r.HostId)
                .HasConversion(
                    id => id.Value,
                    value => HostId.Create(value));

            rb.Property(r => r.DinnerId)
                .HasConversion(
                    id => id.Value,
                    value => DinnerId.Create(value));

            rb.OwnsOne(r => r.Rating);

            rb.OwnsOneAuditState();
        });

        builder.Metadata.FindNavigation(nameof(Guest.Ratings))
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureGuestMenuReviewIdsTable(EntityTypeBuilder<Guest> builder)
    {
        builder.OwnsMany(d => d.MenuReviewIds, dib =>
        {
            dib.ToTable("GuestMenuReviewIds");

            dib.WithOwner().HasForeignKey("GuestId");

            dib.HasKey("Id");

            dib.Property(di => di.Value)
                .HasColumnName("MenuReviewId");
        });

        builder.Metadata.FindNavigation(nameof(Guest.MenuReviewIds))
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureGuestBillIdsTable(EntityTypeBuilder<Guest> builder)
    {
        builder.OwnsMany(d => d.BillIds, dib =>
        {
            dib.ToTable("GuestBillIds");

            dib.WithOwner().HasForeignKey("GuestId");

            dib.HasKey("Id");

            dib.Property(di => di.Value)
                .HasColumnName("BillId");
        });

        builder.Metadata.FindNavigation(nameof(Guest.BillIds))
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureGuestPendingDinnerIdsTable(EntityTypeBuilder<Guest> builder)
    {
        builder.OwnsMany(d => d.PendingDinnerIds, dib =>
        {
            dib.ToTable("GuestPendingDinnerIds");

            dib.WithOwner().HasForeignKey("GuestId");

            dib.HasKey("Id");

            dib.Property(di => di.Value)
                .HasColumnName("DinnerId");
        });

        builder.Metadata.FindNavigation(nameof(Guest.PendingDinnerIds))
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureGuestPastDinnerIdsTable(EntityTypeBuilder<Guest> builder)
    {
        builder.OwnsMany(d => d.PastDinnerIds, dib =>
        {
            dib.ToTable("GuestPastDinnerIds");

            dib.WithOwner().HasForeignKey("GuestId");

            dib.HasKey("Id");

            dib.Property(di => di.Value)
                .HasColumnName("DinnerId");
        });

        builder.Metadata.FindNavigation(nameof(Guest.PastDinnerIds))
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureGuestUpcomingDinnerIdsTable(EntityTypeBuilder<Guest> builder)
    {
        builder.OwnsMany(d => d.UpcomingDinnerIds, dib =>
        {
            dib.ToTable("GuestUpcomingDinnerIds");

            dib.WithOwner().HasForeignKey("GuestId");

            dib.HasKey("Id");

            dib.Property(di => di.Value)
                .HasColumnName("DinnerId");
        });

        builder.Metadata.FindNavigation(nameof(Guest.UpcomingDinnerIds))
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureGuestsTable(EntityTypeBuilder<Guest> builder)
    {
        builder.ToTable("Guests");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(
                id => id.Value,
                value => GuestId.Create(value));

        builder.Property(g => g.FirstName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(g => g.LastName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(g => g.UserId)
            .HasConversion(
                id => id.Value,
                value => UserId.Create(value));

        builder.OwnsOneAuditState();
    }
}