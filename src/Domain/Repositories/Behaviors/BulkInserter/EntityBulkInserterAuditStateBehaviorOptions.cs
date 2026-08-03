// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Configures the audit identity written by <see cref="EntityBulkInserterAuditStateBehavior{TEntity}"/>.
/// </summary>
/// <example>
/// <code>
/// builder.WithBehavior&lt;EntityBulkInserterAuditStateBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterAuditStateBehaviorOptions
{
    /// <summary>
    /// Gets or sets the current-user value written to new audit states.
    /// </summary>
    public AuditStateByType ByType { get; set; } = AuditStateByType.ByUserName;
}
