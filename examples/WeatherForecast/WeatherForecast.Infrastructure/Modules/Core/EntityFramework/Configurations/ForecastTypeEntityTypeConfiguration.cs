// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ForecastTypeEntityTypeConfiguration : IEntityTypeConfiguration<ForecastType>
{
    public void Configure(EntityTypeBuilder<ForecastType> builder)
    {
        builder.ToTable("ForecastTypes");

        builder.HasKey(x => x.Id);

        builder.HasIndex(e => e.Name);

        builder.Property(nameof(ForecastType.Name))
            .HasMaxLength(1048)
            .IsRequired(false);

        builder.Property(nameof(ForecastType.Description))
            .HasMaxLength(2048)
            .IsRequired(false);
    }
}