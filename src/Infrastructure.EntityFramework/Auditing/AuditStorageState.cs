// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

/// <summary>Provider-owned indexed lifecycle membership outside the captured logical document.</summary>
/// <example><code>entity.StorageState = AuditStorageState.Archived;</code></example>
public enum AuditStorageState
{
    /// <summary>Retained evidence in the ordinary active dataset.</summary>
    Active = 0,

    /// <summary>Retained complete evidence available only through explicit archive selection or exact lookup.</summary>
    Archived = 1
}
