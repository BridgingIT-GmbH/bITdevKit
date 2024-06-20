// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public static class EntityTypeBuilderExtensions
{
    public static EntityTypeBuilder<TEntity> OwnsOneAuditState<TEntity>(this EntityTypeBuilder<TEntity> builder) // TODO: also provide a ToJson variant
        where TEntity : class, IEntity, IAuditable
    {
        var valueComparer = new ValueComparer<string[]>(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToArray());

        builder.OwnsOne(e => e.AuditState, o =>
        {
            o.Property(e => e.CreatedBy)
             .HasMaxLength(256);

            o.Property(e => e.CreatedDate);

            o.Property(e => e.CreatedDescription)
             .HasMaxLength(1024);

            o.Property(e => e.UpdatedBy)
             .HasMaxLength(256);

            o.Property(e => e.UpdatedDate);

            o.Property(e => e.UpdatedDescription)
             .HasMaxLength(1024);

            o.Property(e => e.UpdatedReasons)
             .HasConversion<StringsArraySemicolonConverter>() // TODO: .NET8 use new ef core primitive collections here (store as json) https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#primitive-collections
             .HasMaxLength(8192)
             .Metadata.SetValueComparer(valueComparer);

            o.Property(e => e.DeactivatedBy)
             .HasMaxLength(256);

            o.Property(e => e.DeactivatedDate);

            o.Property(e => e.DeactivatedDescription)
               .HasMaxLength(1024);

            o.Property(e => e.DeactivatedReasons)
                .HasConversion<StringsArraySemicolonConverter>() // TODO: .NET8 use new ef core primitive collections here (store as json) https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#primitive-collections
                .HasMaxLength(8192)
                .Metadata.SetValueComparer(valueComparer);

            o.Property(e => e.DeletedBy)
             .HasMaxLength(256);

            o.Property(e => e.DeletedDate);

            o.Property(e => e.DeletedReason)
             .HasMaxLength(1024);

            o.Property(e => e.DeletedDescription)
             .HasMaxLength(1024);
        });

        return builder;
    }

    public static OwnedNavigationBuilder<TOwnerEntity, TEntity> OwnsOneAuditState<TOwnerEntity, TEntity>(this OwnedNavigationBuilder<TOwnerEntity, TEntity> builder) // TODO: also provide a ToJson variant
        where TOwnerEntity : class
        where TEntity : class, IEntity, IAuditable
    {
        var valueComparer = new ValueComparer<string[]>(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToArray());

        builder.OwnsOne(e => e.AuditState, o =>
        {
            o.Property(e => e.CreatedBy)
             .HasMaxLength(256);

            o.Property(e => e.CreatedDate);

            o.Property(e => e.CreatedDescription)
             .HasMaxLength(1024);

            o.Property(e => e.UpdatedBy)
             .HasMaxLength(256);

            o.Property(e => e.UpdatedDate);

            o.Property(e => e.UpdatedDescription)
             .HasMaxLength(1024);

            o.Property(e => e.UpdatedReasons)
                .HasConversion<StringsArraySemicolonConverter>() // TODO: .NET8 use new ef core primitive collections here (store as json) https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#primitive-collections
                .HasMaxLength(8192)
                .Metadata.SetValueComparer(valueComparer);

            o.Property(e => e.DeactivatedBy)
             .HasMaxLength(256);

            o.Property(e => e.DeactivatedDate);

            o.Property(e => e.DeactivatedDescription)
             .HasMaxLength(1024);

            o.Property(e => e.DeactivatedReasons)
                .HasConversion<StringsArraySemicolonConverter>() // TODO: .NET8 use new ef core primitive collections here (store as json) https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#primitive-collections
                .HasMaxLength(8192)
                .Metadata.SetValueComparer(valueComparer);

            o.Property(e => e.DeletedBy)
             .HasMaxLength(256);

            o.Property(e => e.DeletedDate);

            o.Property(e => e.DeletedReason)
             .HasMaxLength(1024);

            o.Property(e => e.DeletedDescription)
             .HasMaxLength(1024);
        });

        return builder;
    }
}