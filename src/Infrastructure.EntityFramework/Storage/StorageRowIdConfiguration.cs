// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>Defines the shared relational representation of blob and document row IDs.</summary>
/// <example><code>StorageRowIdConfiguration.Configure(builder.Property(row => row.Id));</code></example>
public static class StorageRowIdConfiguration
{
    private static readonly ValueConverter<Guid, string> Converter = new(
        id => id.ToString("N"),
        value => Guid.ParseExact(value, "N"));

    /// <summary>Stores a GUID as 32 lowercase hexadecimal characters in a fixed-width non-Unicode column.</summary>
    /// <param name="property">The builder for a storage primary or foreign key.</param>
    /// <returns>The configured property builder.</returns>
    /// <example><code>StorageRowIdConfiguration.Configure(builder.Property(row => row.Id)).ValueGeneratedNever();</code></example>
    public static PropertyBuilder<Guid> Configure(PropertyBuilder<Guid> property)
        => property.HasConversion(Converter)
            .HasColumnType("char(32)")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsFixedLength();

    internal static string GetBytewiseCollation(string providerName) => providerName switch
    {
        "Microsoft.EntityFrameworkCore.SqlServer" => "Latin1_General_100_BIN2",
        "Npgsql.EntityFrameworkCore.PostgreSQL" => "C",
        "Microsoft.EntityFrameworkCore.Sqlite" => "BINARY",
        _ => null
    };
}
