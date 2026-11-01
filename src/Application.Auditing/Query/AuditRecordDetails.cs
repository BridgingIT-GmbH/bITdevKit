// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>A permitted response projection with independently evaluated source-integrity and storage metadata.</summary>
/// <example><code>var originalStatus = details.IntegrityStatus;</code></example>
public sealed record AuditRecordDetails
{
    /// <summary>The original or policy-restricted immutable business evidence.</summary>
    /// <example><code>var actions = details.Record.Actions;</code></example>
    public AuditRecord Record { get; init; }

    /// <summary>Provider lifecycle state outside the captured document.</summary>
    /// <example><code>var archived = details.StorageState == "Archived";</code></example>
    public string StorageState { get; init; }

    /// <summary>Verification of the complete source, never a signature over the response.</summary>
    /// <example><code>var verified = details.IntegrityStatus == "Verified";</code></example>
    public string IntegrityStatus { get; init; } = "NotChecked";

    /// <summary>Whether current response policy transformed the original evidence.</summary>
    /// <example><code>var changed = details.IsTransformed;</code></example>
    public bool IsTransformed { get; init; }
}
