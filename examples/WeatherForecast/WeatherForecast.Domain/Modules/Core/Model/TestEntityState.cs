// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

using System.Globalization;
using Common;

public class TestEntityState
{
    public string CreatedBy { get; private set; }

    public DateTime CreatedDate { get; private set; } = DateTime.UtcNow;

    public string CreatedDescription { get; private set; }

    public string UpdatedBy { get; private set; }

    public DateTime? UpdatedDate { get; private set; }

    public string UpdatedDescription { get; set; }

    public IEnumerable<string> UpdatedReasons { get; private set; }

    public bool? Deactivated { get; private set; }

    public IEnumerable<string> DeactivatedReasons { get; private set; }

    public string DeactivatedBy { get; private set; }

    public DateTimeOffset? DeactivatedDate { get; private set; }

    public string DeactivatedDescription { get; set; }

    public bool? Deleted { get; private set; }

    public string DeletedBy { get; private set; }

    public DateTime? DeletedDate { get; private set; }

    public string DeletedReason { get; private set; }

    public string DeletedDescription { get; set; }

    /// <summary>
    ///     Gets the last date this instance was changed
    /// </summary>
    public DateTime? LastActionDate =>
        new List<DateTime?> { this.CreatedDate, this.UpdatedDate, this.DeletedDate }
            .Where(d => d is not null)
            .SafeNull()
            .Max();

    /// <summary>
    ///     Gets a value indicating whether determines whether this instance is active.
    /// </summary>
    /// <value>
    ///     <c>true</c> if this instance is deactivated; otherwise, <c>false</c>.
    /// </value>
    public virtual bool IsDeactivated =>
        (this.Deactivated is not null && (bool)this.Deactivated) || !this.DeactivatedReasons.IsNullOrEmpty();

    /// <summary>
    ///     Gets a value indicating whether this instance is deleted.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if this instance is deleted; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool IsDeleted()
    {
        return (this.Deleted is not null && (bool)this.Deleted) || !this.DeletedReason.IsNullOrEmpty();
    }

    /// <summary>
    ///     Sets the created information, also sets the initial status
    /// </summary>
    /// <param name="by">Name of the account of the creater.</param>
    /// <param name="description">The description for the creation.</param>
    public virtual void SetCreated(string by = null, string description = null)
    {
        this.CreatedDate = DateTime.UtcNow;
        if (!by.IsNullOrEmpty())
        {
            this.CreatedBy = by;
        }

        if (!description.IsNullOrEmpty())
        {
            this.CreatedDescription = description;
        }
    }

    /// <summary>
    ///     Sets the updated information.
    /// </summary>
    /// <param name="by">Name of the account of the updater.</param>
    /// <param name="reason">The reason of the update.</param>
    public virtual void SetUpdated(string by = null, string reason = null)
    {
        this.UpdatedDate = DateTime.UtcNow;

        if (!by.IsNullOrEmpty())
        {
            this.UpdatedBy = by;
        }

        if (!reason.IsNullOrEmpty())
        {
            if (this.UpdatedReasons.IsNullOrEmpty())
            {
                this.UpdatedReasons = [];
            }

            this.UpdatedReasons = this.UpdatedReasons.Concat(new[]
            {
                $"{by}: ({this.UpdatedDate.Value.ToString(CultureInfo.InvariantCulture)}) {reason}".Trim()
            });
        }
    }

    /// <summary>
    ///     Sets the deactivated information.
    /// </summary>
    /// <param name="by">Name of the deactivator.</param>
    /// <param name="reason">The reason.</param>
    public virtual void SetDeactivated(string by = null, string reason = null)
    {
        this.DeactivatedDate = DateTimeOffset.UtcNow;
        this.SetUpdated(by);
        this.Deactivated = true;

        if (!by.IsNullOrEmpty())
        {
            this.DeactivatedBy = by;
        }

        if (!reason.IsNullOrEmpty())
        {
            if (this.DeactivatedReasons.IsNullOrEmpty())
            {
                this.DeactivatedReasons = new List<string>();
            }

            this.DeactivatedReasons = this.DeactivatedReasons.Concat(new[]
            {
                $"{by}: ({this.DeactivatedDate.Value.ToString(CultureInfo.InvariantCulture)}) {reason}".Trim()
            });
        }
    }

    /// <summary>
    ///     Sets the deleted information.
    /// </summary>
    /// <param name="by">Name of the deleter.</param>
    /// <param name="reason">The reason.</param>
    public virtual void SetDeleted(string by = null, string reason = null)
    {
        this.Deleted = true;
        this.DeletedDate = DateTime.UtcNow;
        this.UpdatedDate = this.DeletedDate.Value;

        if (!by.IsNullOrEmpty())
        {
            this.DeletedBy = by;
        }

        if (!reason.IsNullOrEmpty())
        {
            this.DeletedReason =
                $"{by}: ({this.DeletedDate.Value.ToString(CultureInfo.InvariantCulture)}) {reason}".Trim();
        }
    }
}