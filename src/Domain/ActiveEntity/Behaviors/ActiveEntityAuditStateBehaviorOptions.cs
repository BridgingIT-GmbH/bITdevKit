// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;
/// <summary>
/// Configuration options for the EntityAuditStateBehavior.
/// </summary>
public class ActiveEntityAuditStateBehaviorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether soft delete is enabled.
    /// </summary>
    public bool SoftDeleteEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether soft delete is enabled.
    /// </summary>
    public ActiveEntityAuditStateBehaviorOptions EnableSoftDelete(bool value = true)
    {
        this.SoftDeleteEnabled = value;
        return this;
    }

    /// <summary>
    /// Gets or sets the identity used for audit fields (e.g., CreatedBy, UpdatedBy).
    /// </summary>
    /// <example>
    /// <code>
    /// var options = new AuditStateBehaviorOptions
    /// {
    ///     SoftDeleteEnabled = true,
    ///     AuditUserIdentity = "system-user"
    /// };
    /// </code>
    /// </example>
    public string AuditUserIdentity { get; set; } = "system";

    /// <summary>
    /// Gets or sets the identity used for audit fields (e.g., CreatedBy, UpdatedBy).
    /// </summary>
    public ActiveEntityAuditStateBehaviorOptions AuditUser(string value)
    {
        this.AuditUserIdentity = value;
        return this;
    }
}
