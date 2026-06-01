// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Model;

/// <summary>
/// Represents a user's subscription to a city, including display ordering and primary city designation.
/// </summary>
[DebuggerDisplay("Id={Id}, UserId={UserId}, CityId={CityId}, IsPrimary={IsPrimary}")]
[TypedEntityId<Guid>]
public class UserCity : ActiveEntity<UserCity, UserCityId>, IAuditable, IConcurrency
{
    /// <summary>Gets or sets the user identifier.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the city identifier.</summary>
    public CityId CityId { get; set; }

    /// <summary>Gets or sets a value indicating whether this is the user's primary city.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Gets or sets the display order for sorting cities in the UI.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Gets or sets the audit state tracking creation, updates, and soft deletes.</summary>
    public AuditState AuditState { get; set; } = new();

    /// <summary>Gets or sets the concurrency version for optimistic concurrency control.</summary>
    public Guid ConcurrencyVersion { get; set; }

    private UserCity() { } // EF Core

    /// <summary>
    /// Creates a new <see cref="UserCity"/> subscription instance.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cityId">The city identifier.</param>
    /// <param name="isPrimary">Whether this is the user's primary city.</param>
    /// <param name="displayOrder">The display order for sorting.</param>
    /// <returns>A new <see cref="UserCity"/> instance.</returns>
    public static UserCity Create(string userId, CityId cityId, bool isPrimary = false, int displayOrder = 0)
    {
        return new UserCity
        {
            UserId = userId,
            CityId = cityId,
            IsPrimary = isPrimary,
            DisplayOrder = displayOrder
        };
    }

    /// <summary>
    /// Soft-deletes this subscription, clearing the primary flag.
    /// </summary>
    /// <param name="reason">The optional reason for deletion.</param>
    public void SoftDelete(string reason = null)
    {
        this.AuditState.SetDeleted("user", reason);
        this.IsPrimary = false;
    }

    /// <summary>
    /// Reactivates a previously soft-deleted subscription.
    /// </summary>
    public void Reactivate()
    {
        this.AuditState.SetUndeleted();
    }

    /// <summary>
    /// Sets or clears the primary city designation.
    /// </summary>
    /// <param name="isPrimary">Whether this city should be primary.</param>
    public void SetPrimary(bool isPrimary)
    {
        this.IsPrimary = isPrimary;
    }

    /// <summary>
    /// Sets the display order for this subscription.
    /// </summary>
    /// <param name="order">The display order value.</param>
    public void SetDisplayOrder(int order)
    {
        this.DisplayOrder = order;
    }
}
