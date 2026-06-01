// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

/// <summary>
/// EF Core entity type configuration for the <see cref="City"/> aggregate.
/// </summary>
public class CityEntityTypeConfiguration : IEntityTypeConfiguration<City>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => CityId.Create(value));

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Country)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.TimeZone)
            .IsRequired()
            .HasMaxLength(50);

        builder.OwnsOne(x => x.Location, lb =>
        {
            lb.Property(l => l.Latitude)
                .HasColumnName("Latitude")
                .HasColumnType("decimal(10,7)")
                .IsRequired();

            lb.Property(l => l.Longitude)
                .HasColumnName("Longitude")
                .HasColumnType("decimal(10,7)")
                .IsRequired();
        });

        builder.Property(x => x.Elevation)
            .HasColumnType("decimal(8,2)");

        builder.Property(x => x.ExternalId);

        builder.HasIndex(x => x.ExternalId)
            .IsUnique()
            .HasFilter("[ExternalId] IS NOT NULL");

        builder.OwnsOneAuditState();
    }
}
