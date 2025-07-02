// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Globalization;

/// <summary>
///     Represents an audit state which captures the creation, update, deactivation, and deletion details
///     of an entity. This includes timestamps, user information, descriptions, and reasons for changes.
/// </summary>
public class AuditState
{
    /// <summary>
    ///     Gets the identity of the user who created this entity.
    /// </summary>
    public string CreatedBy { get; private set; }

    /// <summary>
    ///     Gets the date and time when this entity was created.
    /// </summary>
    public DateTimeOffset CreatedDate { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Gets the description of when and why this entity was created.
    /// </summary>
    public string CreatedDescription { get; private set; }

    /// <summary>
    ///     Gets the identifier of the user or system that last updated this entity.
    /// </summary>
    public string UpdatedBy { get; private set; }

    /// <summary>
    ///     Gets the date this entity was last updated.
    /// </summary>
    public DateTimeOffset? UpdatedDate { get; private set; }

    /// <summary>
    ///     Gets or sets the description associated with the last update.
    /// </summary>
    public string UpdatedDescription { get; set; }

    /// <summary>
    ///     Gets the reasons associated with the updates to this entity.
    /// </summary>
    public string[] UpdatedReasons { get; private set; }

    /// <summary>
    ///     Gets the value indicating whether this entity is deactivated.
    /// </summary>
    public bool? Deactivated { get; private set; }

    /// <summary>
    ///     Gets the reasons why this entity was deactivated.
    /// </summary>
    public string[] DeactivatedReasons { get; private set; }

    /// <summary>
    ///     Gets the identifier of the user or entity who deactivated the instance
    /// </summary>
    public string DeactivatedBy { get; private set; }

    /// <summary>
    ///     Gets the date on which the instance was deactivated.
    /// </summary>
    public DateTimeOffset? DeactivatedDate { get; private set; }

    /// <summary>
    ///     Gets or sets the description for why the entity was deactivated.
    /// </summary>
    public string DeactivatedDescription { get; set; }

    /// <summary>
    ///     Gets a value indicating whether this entity has been marked as deleted.
    /// </summary>
    public bool? Deleted { get; private set; }

    /// <summary>
    ///     Gets the identifier of the user who deleted this entity
    /// </summary>
    public string DeletedBy { get; private set; }

    /// <summary>
    ///     Gets the date this entity was marked as deleted.
    /// </summary>
    public DateTimeOffset? DeletedDate { get; private set; }

    /// <summary>
    ///     Gets the reason why the instance was deleted
    /// </summary>
    public string DeletedReason { get; private set; }

    /// <summary>
    ///     Gets or sets the description for the deletion action
    /// </summary>
    public string DeletedDescription { get; set; }

    /// <summary>
    ///     Provides the date and time of the most recent action performed on this entity.
    /// </summary>
    public DateTimeOffset? LastActionDate =>
        new List<DateTimeOffset?> { this.CreatedDate, this.UpdatedDate, this.DeletedDate, this.DeactivatedDate }
            .Where(d => d is not null).SafeNull().Max();

    /// <summary>
    ///     Gets a value indicating whether this entity is deactivated.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if this entity is deactivated; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool IsDeactivated()
    {
        return (this.Deactivated is not null && (bool)this.Deactivated) || this.IsDeleted();
    }

    /// <summary>
    ///     Gets a value indicating whether this entity is activated.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if this entity is activated; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool IsActive()
    {
        return (this.Deactivated is null || !(bool)this.Deactivated) && !this.IsDeleted();
    }

    /// <summary>
    ///     Gets a value indicating whether this entity is deleted.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if this entity is deleted; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool IsDeleted()
    {
        return (this.Deleted is not null && (bool)this.Deleted);
    }

    /// <summary>
    ///     Gets a value indicating whether this entity has been updated.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if this entity is updated; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool IsUpdated()
    {
        return this.UpdatedDate is not null && this.UpdatedDate.HasValue;
    }

    /// <summary>
    ///     Sets the created information, also sets the initial status.
    /// </summary>
    /// <param name="by">Name of the account of the creator.</param>
    /// <param name="description">The description for the creation.</param>
    public virtual void SetCreated(string by = null, string description = null)
    {
        this.CreatedDate = DateTimeOffset.UtcNow;
        this.UpdatedDate = this.CreatedDate;

        if (!by.IsNullOrEmpty() && string.IsNullOrEmpty(this.CreatedBy))
        {
            this.CreatedBy = by;
        }

        if (!description.IsNullOrEmpty() && string.IsNullOrEmpty(this.CreatedDescription))
        {
            this.CreatedDescription = description;
        }
    }

    /// <summary>
    ///     Sets the updated information.
    /// </summary>
    /// <param name="by">Name of the account of the updater.</param>
    /// <param name="reason">The reason for the update.</param>
    public virtual void SetUpdated(string by = null, string reason = null)
    {
        this.UpdatedDate = DateTimeOffset.UtcNow;

        if (!by.IsNullOrEmpty())
        {
            this.UpdatedBy = by;
        }

        if (!reason.IsNullOrEmpty())
        {
            if (this.UpdatedReasons.IsNullOrEmpty())
            {
                this.UpdatedReasons = Enumerable.Empty<string>().ToArray();
            }

            this.UpdatedReasons =
            [
                .. this.UpdatedReasons,
                .. new[]
                {
                    $"{by}: ({this.UpdatedDate.Value.ToString(CultureInfo.InvariantCulture)}) {reason}".Trim()
                }
            ];
        }
    }

    /// <summary>
    ///     Sets the deactivated information.
    /// </summary>
    /// <param name="by">Name of the deactivator.</param>
    /// <param name="reason">The reason for deactivation.</param>
    public virtual void SetDeactivated(string by = null, string reason = null)
    {
        this.DeactivatedDate = DateTimeOffset.UtcNow;
        this.Deactivated = true;
        this.UpdatedDate = this.DeactivatedDate;

        if (!by.IsNullOrEmpty())
        {
            this.DeactivatedBy = by;
        }

        if (!reason.IsNullOrEmpty())
        {
            if (this.DeactivatedReasons.IsNullOrEmpty())
            {
                this.DeactivatedReasons = Enumerable.Empty<string>().ToArray();
            }

            this.DeactivatedReasons =
            [
                .. this.DeactivatedReasons,
                .. new[]
                {
                    $"{by}: ({this.DeactivatedDate.Value.ToString(CultureInfo.InvariantCulture)}) {reason}".Trim()
                }
            ];
        }
    }

    /// <summary>
    ///     Sets the activated information (reverses deactivation).
    /// </summary>
    /// <param name="by">Name of the activator.</param>
    /// <param name="reason">The reason for activation.</param>
    public virtual void SetActivated(string by = null, string reason = null)
    {
        var now = DateTimeOffset.UtcNow;
        this.Deactivated = false;
        this.UpdatedDate = now;
        this.DeactivatedDate = null;
        this.DeactivatedBy = null;

        if (!by.IsNullOrEmpty())
        {
            this.UpdatedBy = by;
        }

        if (!reason.IsNullOrEmpty())
        {
            if (this.UpdatedReasons.IsNullOrEmpty())
            {
                this.UpdatedReasons = Enumerable.Empty<string>().ToArray();
            }

            this.UpdatedReasons =
            [
                .. this.UpdatedReasons,
                .. new[]
                {
                    $"{by}: ({now.ToString(CultureInfo.InvariantCulture)}) Activated: {reason}".Trim()
                }
            ];
        }
    }

    /// <summary>
    ///     Sets the deleted information.
    /// </summary>
    /// <param name="by">Name of the deleter.</param>
    /// <param name="reason">The reason for deletion.</param>
    public virtual void SetDeleted(string by = null, string reason = null)
    {
        this.Deleted = true;
        this.DeletedDate = DateTimeOffset.UtcNow;
        this.UpdatedDate = this.DeletedDate;

        if (!by.IsNullOrEmpty())
        {
            this.DeletedBy = by;
        }

        if (!reason.IsNullOrEmpty())
        {
            this.DeletedReason = $"{by}: ({this.DeletedDate.Value.ToString(CultureInfo.InvariantCulture)}) {reason}".Trim();
        }
    }

    /// <summary>
    ///     Sets the undeleted information (reverses deletion).
    /// </summary>
    /// <param name="by">Name of the user who undeleted the entity.</param>
    /// <param name="reason">The reason for undeletion.</param>
    public virtual void SetUndeleted(string by = null, string reason = null)
    {
        var now = DateTimeOffset.UtcNow;
        this.Deleted = false;
        this.DeletedDate = null;
        this.UpdatedDate = now;
        this.DeletedReason = null;
        this.DeletedBy = null;

        if (!by.IsNullOrEmpty())
        {
            this.UpdatedBy = by;
        }

        if (!reason.IsNullOrEmpty())
        {
            if (this.UpdatedReasons.IsNullOrEmpty())
            {
                this.UpdatedReasons = Enumerable.Empty<string>().ToArray();
            }

            this.UpdatedReasons =
            [
                .. this.UpdatedReasons,
                .. new[]
                {
                    $"{by}: ({now.ToString(CultureInfo.InvariantCulture)}) Undeleted: {reason}".Trim()
                }
            ];
        }
    }
}