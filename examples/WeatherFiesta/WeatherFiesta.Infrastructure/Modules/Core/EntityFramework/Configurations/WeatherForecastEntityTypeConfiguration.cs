// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// EF Core entity type configuration for the <see cref="WeatherForecast"/> aggregate.
/// </summary>
public class WeatherForecastEntityTypeConfiguration : IEntityTypeConfiguration<WeatherForecast>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WeatherForecast> builder)
    {
        builder.ToTable("WeatherForecasts").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => WeatherForecastId.Create(value));

        builder.Property(x => x.CityId)
            .HasConversion(id => id.Value, value => CityId.Create(value))
            .IsRequired();

        builder.Property(x => x.ForecastDate)
            .IsRequired();

        builder.Property(x => x.DayWeatherCode)
            .IsRequired();

        builder.Property(x => x.TemperatureMax)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.TemperatureMin)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.ApparentTemperatureMax)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.ApparentTemperatureMin)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.PrecipitationSum)
            .HasColumnType("decimal(5,3)")
            .IsRequired();

        builder.Property(x => x.PrecipitationProbabilityMax)
            .IsRequired();

        builder.Property(x => x.WindSpeedMax)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.WindGustsMax)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.DominantWindDirection)
            .IsRequired();

        builder.Property(x => x.UvIndexMax)
            .HasColumnType("decimal(3,1)")
            .IsRequired();

        builder.Property(x => x.SunshineDurationSeconds)
            .IsRequired();

        builder.Property(x => x.DaylightDurationSeconds)
            .IsRequired();

        builder.Property(x => x.Sunrise)
            .IsRequired();

        builder.Property(x => x.Sunset)
            .IsRequired();

        builder.Property(x => x.RetrievedAt)
            .IsRequired();

        builder.OwnsMany(x => x.HourlyForecasts, h =>
        {
            h.Property(hf => hf.Hour).HasColumnName("Hour");
            h.Property(hf => hf.Temperature).HasColumnName("Temperature").HasColumnType("decimal(5,2)");
            h.Property(hf => hf.RelativeHumidity).HasColumnName("RelativeHumidity");
            h.Property(hf => hf.ApparentTemperature).HasColumnName("ApparentTemperature").HasColumnType("decimal(5,2)");
            h.Property(hf => hf.PrecipitationProbability).HasColumnName("PrecipitationProbability");
            h.Property(hf => hf.Precipitation).HasColumnName("Precipitation").HasColumnType("decimal(5,3)");
            h.Property(hf => hf.WeatherCode).HasColumnName("WeatherCode");
            h.Property(hf => hf.WindSpeed).HasColumnName("WindSpeed").HasColumnType("decimal(5,2)");
            h.Property(hf => hf.WindDirection).HasColumnName("WindDirection");
            h.Property(hf => hf.WindGusts).HasColumnName("WindGusts").HasColumnType("decimal(5,2)");
            h.Property(hf => hf.CloudCover).HasColumnName("CloudCover");
            h.Property(hf => hf.Visibility).HasColumnName("Visibility").HasColumnType("decimal(5,2)");
            h.Property(hf => hf.IsDay).HasColumnName("IsDay");
        });

        builder.HasIndex(x => new { x.CityId, x.ForecastDate })
            .IsUnique();

        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
