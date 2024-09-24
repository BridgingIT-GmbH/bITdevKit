// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure;

using DevKit.Infrastructure.EntityFramework;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class MenuReviewEntityTypeConfiguration : IEntityTypeConfiguration<MenuReview>
{
    public void Configure(EntityTypeBuilder<MenuReview> builder)
    {
        builder.ToTable("MenuReviews");

        builder.HasKey(mr => mr.Id);

        builder.Property(mr => mr.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value,
                value => MenuReviewId.Create(value));

        builder.OwnsOne(mr => mr.Rating);

        builder.Property(mr => mr.Comment)
            .HasMaxLength(512);

        builder.Property(mr => mr.HostId)
            .HasConversion(id => id.Value,
                value => HostId.Create(value));

        builder.Property(mr => mr.MenuId)
            .HasConversion(id => id.Value,
                value => MenuId.Create(value));

        builder.Property(mr => mr.GuestId)
            .HasConversion(id => id.Value,
                value => GuestId.Create(value));

        builder.Property(mr => mr.DinnerId)
            .HasConversion(id => id.Value,
                value => DinnerId.Create(value));

        builder.OwnsOneAuditState();
    }
}