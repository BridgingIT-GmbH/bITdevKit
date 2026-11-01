// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Cumulative delivery through a fixed acceptance boundary.</summary>
/// <example><code>var value = new AuditFlushResult();</code></example>
public sealed record AuditFlushResult
{
    /// <summary>Process receipt epoch, not a durable store epoch.</summary>
    /// <example><code>var value = snapshot.ReceiptEpoch;</code></example>
    public Guid ReceiptEpoch { get; init; }

    /// <summary>Covered admission boundary.</summary>
    /// <example><code>var value = snapshot.ThroughSequence;</code></example>
    public long ThroughSequence { get; init; }

    /// <summary>Covered successful admissions.</summary>
    /// <example><code>var value = snapshot.Persisted;</code></example>
    public long Persisted { get; init; }

    /// <summary>Covered terminal failures, including retained aggregate history.</summary>
    /// <example><code>var value = snapshot.Failed;</code></example>
    public long Failed { get; init; }

    /// <summary>Covered admissions not yet confirmed.</summary>
    /// <example><code>var value = snapshot.Pending;</code></example>
    public long Pending { get; init; }

}
