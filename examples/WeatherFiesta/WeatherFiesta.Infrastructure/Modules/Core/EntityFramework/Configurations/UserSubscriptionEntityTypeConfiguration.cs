// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

/// <summary>
/// EF Core entity type configuration for the <see cref="UserSubscription"/> aggregate.
/// </summary>
public class UserSubscriptionEntityTypeConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("Subscriptions").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => UserSubscriptionId.Create(value));

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Plan)
            .HasConversion(new EnumerationConverter<SubscriptionPlan>())
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(new EnumerationConverter<SubscriptionStatus>())
            .IsRequired();

        builder.Property(x => x.BillingCycle)
            .HasConversion(new EnumerationConverter<SubscriptionBillingCycle>())
            .IsRequired();

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.EndDate);

        // One subscription per user
        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.OwnsOneAuditState();
    }
}
