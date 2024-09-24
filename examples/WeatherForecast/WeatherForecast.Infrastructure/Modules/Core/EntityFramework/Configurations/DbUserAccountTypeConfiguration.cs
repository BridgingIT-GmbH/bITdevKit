// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DbUserAccountTypeConfiguration : IEntityTypeConfiguration<DbUserAccount>
{
    public void Configure(EntityTypeBuilder<DbUserAccount> builder)
    {
        builder.ToTable("UserAccounts");

        builder.HasKey(x => x.Identifier);

        builder.Property(e => e.EmailAddress)
            .IsRequired()
            .HasMaxLength(1024);
    }
}