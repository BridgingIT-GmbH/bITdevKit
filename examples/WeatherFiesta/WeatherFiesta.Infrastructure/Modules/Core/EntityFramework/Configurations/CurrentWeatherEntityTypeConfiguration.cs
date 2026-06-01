// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// EF Core entity type configuration for the <see cref="CurrentWeather"/> aggregate.
/// </summary>
public class CurrentWeatherEntityTypeConfiguration : IEntityTypeConfiguration<CurrentWeather>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CurrentWeather> builder)
    {
        builder.ToTable("CurrentWeathers").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => CurrentWeatherId.Create(value));

        builder.Property(x => x.CityId)
            .HasConversion(id => id.Value, value => CityId.Create(value))
            .IsRequired();

        builder.Property(x => x.Temperature)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.ApparentTemperature)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.Humidity)
            .IsRequired();

        builder.Property(x => x.WeatherCode)
            .IsRequired();

        builder.Property(x => x.WindSpeed)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.WindDirection)
            .IsRequired();

        builder.Property(x => x.WindGusts)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.Precipitation)
            .HasColumnType("decimal(5,3)")
            .IsRequired();

        builder.Property(x => x.CloudCover)
            .IsRequired();

        builder.Property(x => x.Pressure)
            .HasColumnType("decimal(7,2)")
            .IsRequired();

        builder.Property(x => x.RetrievedAt)
            .IsRequired();

        builder.HasIndex(x => x.CityId)
            .IsUnique();

        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
