// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Source-owned, bounded static auditing registration metadata.</summary>
/// <example><code>var selected = source.Registrations.Where(r => r.Selection == "selected");</code></example>
public interface IAuditBoundaryRegistrationSource
{
    /// <summary>Copied safe descriptors without resolving or invoking business handlers.</summary>
    /// <example><code>var count = source.Registrations.Count;</code></example>
    IReadOnlyList<AuditBoundaryRegistration> Registrations { get; }
}
