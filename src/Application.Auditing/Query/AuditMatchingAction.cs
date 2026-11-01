// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>A bounded retained resource-match occurrence for application history.</summary>
/// <example><code>var value = new AuditMatchingAction();</code></example>
public sealed record AuditMatchingAction
{
    /// <summary>Distinct matching action occurrence.</summary>
    /// <example><code>var value = snapshot.ActionId;</code></example>
    public Guid ActionId { get; init; }

    /// <summary>Captured parent identity.</summary>
    /// <example><code>var value = snapshot.ParentActionId;</code></example>
    public Guid? ParentActionId { get; init; }

    /// <summary>Captured business key.</summary>
    /// <example><code>var value = snapshot.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Captured label.</summary>
    /// <example><code>var value = snapshot.DisplayName;</code></example>
    public string DisplayName { get; init; }

    /// <summary>Observed action outcome.</summary>
    /// <example><code>var value = snapshot.Outcome;</code></example>
    public AuditOutcome Outcome { get; init; }

    /// <summary>Observed action start.</summary>
    /// <example><code>var value = snapshot.StartedUtc;</code></example>
    public DateTimeOffset StartedUtc { get; init; }
}
