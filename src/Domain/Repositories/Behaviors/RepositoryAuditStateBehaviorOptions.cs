// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Configures repository audit state behavior.
/// </summary>
public class RepositoryAuditStateBehaviorOptions
{
    /// <summary>
    /// Gets or sets the by type.
    /// </summary>
    public AuditStateByType ByType { get; set; } = AuditStateByType.ByUserName;

    /// <summary>
    /// Gets or sets the soft delete enabled.
    /// </summary>
    public bool SoftDeleteEnabled { get; set; } = true;
}
