// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ForecastEntityTypeConfiguration : IEntityTypeConfiguration<Forecast>
{
    public void Configure(EntityTypeBuilder<Forecast> builder)
    {
        builder.ToTable("Forecasts");

        builder.HasKey(x => x.Id);

        builder.Property(nameof(Forecast.TemperatureMax))
            .HasColumnType("decimal(5,2)")
            .IsRequired(false);

        builder.Property(nameof(Forecast.TemperatureMin))
            .HasColumnType("decimal(5,2)")
            .IsRequired(false);

        builder.Property(nameof(Forecast.WindSpeed))
            .HasColumnType("decimal(5,2)")
            .IsRequired(false);

        builder.HasOne(e => e.Type)
            .WithMany()
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
    }
}