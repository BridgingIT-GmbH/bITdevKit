// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure;

using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class HostEntityTypeConfiguration : IEntityTypeConfiguration<Host>
{
    public void Configure(EntityTypeBuilder<Host> builder)
    {
        ConfigureHostsTable(builder);
        ConfigureHostMenuIdsTable(builder);
        ConfigureHostDinnerIdsTable(builder);
    }

    private static void ConfigureHostMenuIdsTable(EntityTypeBuilder<Host> builder)
    {
        builder.OwnsMany(h => h.MenuIds, mb =>
        {
            mb.WithOwner().HasForeignKey("HostId");

            mb.ToTable("HostMenuIds");

            mb.HasKey("Id");

            mb.Property(mi => mi.Value)
                .ValueGeneratedNever()
                .HasColumnName("HostMenuId");
        });

        builder.Metadata.FindNavigation(nameof(Host.MenuIds))
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureHostDinnerIdsTable(EntityTypeBuilder<Host> builder)
    {
        builder.OwnsMany(h => h.DinnerIds, mb =>
        {
            mb.WithOwner().HasForeignKey("HostId");

            mb.ToTable("HostDinnerIds");

            mb.HasKey("Id");

            mb.Property(mi => mi.Value)
                .ValueGeneratedNever()
                .HasColumnName("HostDinnerId");
        });

        builder.Metadata.FindNavigation(nameof(Host.DinnerIds))
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureHostsTable(EntityTypeBuilder<Host> builder)
    {
        builder.ToTable("Hosts");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(
                id => id.Value,
                value => HostId.Create(value));

        builder.Property(h => h.FirstName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(h => h.LastName)
            .IsRequired()
            .HasMaxLength(128);

        builder.OwnsOne(h => h.AverageRating, ab =>
        {
            ab.Property(e => e.Value)
                .IsRequired(false);

            ab.Property(e => e.NumRatings);
        });

        builder.Property(h => h.UserId)
            .HasConversion(
                id => id.Value,
                value => UserId.Create(value));

        builder.OwnsOneAuditState();
    }
}