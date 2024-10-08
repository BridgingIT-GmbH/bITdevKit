﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using DevKit.Infrastructure.EntityFramework;
using Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TestGuidEntityTypeConfiguration : IEntityTypeConfiguration<TestGuidEntity>
{
    public void Configure(EntityTypeBuilder<TestGuidEntity> builder)
    {
        builder.ToTable("TestGuidEntities");

        builder.HasKey(x => x.Id);

        builder.Property(e => e.MyProperty1)
            .IsRequired(false)
            .HasMaxLength(256);

        builder.Property(e => e.MyProperty2)
            .IsRequired(false)
            .HasMaxLength(256);

        builder.Property(e => e.MyProperty3)
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasMany(e => e.Children)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOneAuditState();
    }
}