// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>A process-epoch delivery acknowledgment, distinct from durable extraction progress.</summary>
/// <example><code>var value = new AuditAppendReceipt();</code></example>
public sealed record AuditAppendReceipt
{
    /// <summary>Process-instance receipt epoch.</summary>
    /// <example><code>var value = snapshot.ReceiptEpoch;</code></example>
    public Guid ReceiptEpoch { get; init; }

    /// <summary>Admission boundary within the receipt epoch.</summary>
    /// <example><code>var value = snapshot.AcceptanceSequence;</code></example>
    public long AcceptanceSequence { get; init; }

    /// <summary>Frozen document identity.</summary>
    /// <example><code>var value = snapshot.AuditId;</code></example>
    public Guid AuditId { get; init; }

    /// <summary>Queued, Persisted, or AlreadyPresent; only the latter two confirm durable presence for durable stores.</summary>
    /// <example><code>var value = snapshot.State;</code></example>
    public string State { get; init; } = "Persisted";

}
