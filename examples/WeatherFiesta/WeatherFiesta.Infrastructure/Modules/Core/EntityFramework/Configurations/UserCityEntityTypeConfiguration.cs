// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// EF Core entity type configuration for the <see cref="UserCity"/> aggregate.
/// </summary>
public class UserCityEntityTypeConfiguration : IEntityTypeConfiguration<UserCity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserCity> builder)
    {
        builder.ToTable("UserCities").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => UserCityId.Create(value));

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.CityId)
            .HasConversion(id => id.Value, value => CityId.Create(value))
            .IsRequired();

        builder.Property(x => x.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasIndex(x => new { x.UserId, x.CityId })
            .IsUnique();

        builder.HasIndex(x => new { x.UserId, x.DisplayOrder });

        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOneAuditState();
    }
}
