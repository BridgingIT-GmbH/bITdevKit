// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

/// <summary>
/// EF Core entity type configuration for the <see cref="UserProfile"/> aggregate.
/// </summary>
public class UserProfileEntityTypeConfiguration : IEntityTypeConfiguration<UserProfile>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => UserProfileId.Create(value));

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TemperatureUnit)
            .HasConversion(new EnumerationConverter<TemperatureUnit>())
            .IsRequired();

        builder.Property(x => x.WindSpeedUnit)
            .HasConversion(new EnumerationConverter<WindSpeedUnit>())
            .IsRequired();

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.OwnsOneAuditState();
    }
}
