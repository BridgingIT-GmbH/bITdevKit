// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// EF Core entity type configuration for the <see cref="WeatherReport" /> aggregate.
/// </summary>
public class WeatherReportEntityTypeConfiguration : IEntityTypeConfiguration<WeatherReport>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WeatherReport> builder)
    {
        builder.ToTable("WeatherReports").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => WeatherReportId.Create(value));

        builder.Property(e => e.CityId)
            .HasConversion(id => id.Value, value => CityId.Create(value))
            .IsRequired();

        builder.Property(e => e.ReportType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.PeriodStartUtc)
            .IsRequired();

        builder.Property(e => e.PeriodEndUtc)
            .IsRequired();

        builder.Property(e => e.ForecastDateStart)
            .IsRequired();

        builder.Property(e => e.ForecastDateEndExclusive)
            .IsRequired();

        builder.Property(e => e.Summary)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.GeneratedAt)
            .IsRequired();

        builder.HasIndex(e => new
            {
                e.CityId,
                e.ReportType,
                e.PeriodStartUtc,
                e.PeriodEndUtc
            })
            .IsUnique();

        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
