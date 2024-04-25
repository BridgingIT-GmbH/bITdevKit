// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TestIntChildEntityTypeConfiguration : IEntityTypeConfiguration<TestIntChildEntity>
{
    public void Configure(EntityTypeBuilder<TestIntChildEntity> builder)
    {
        builder.ToTable("TestIntChildEntities");

        builder.HasKey(x => x.Id);

        builder.Property(e => e.MyProperty1)
            .IsRequired(false)
            .HasMaxLength(256);

        builder.Property(e => e.MyProperty2)
            .IsRequired(false)
            .HasMaxLength(256);
    }
}