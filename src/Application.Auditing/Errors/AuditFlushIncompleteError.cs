// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Reports safe cumulative failed/pending delivery counts without treating an incomplete flush as success.</summary>
/// <example><code>var pending = error.FlushResult.Pending;</code></example>
public sealed class AuditFlushIncompleteError(AuditFlushResult flushResult) : ResultErrorBase("AuditFlushIncomplete")
{
    /// <summary>Delivery through the exact captured acceptance boundary, including earlier terminal failures.</summary>
    /// <example><code>var failed = error.FlushResult.Failed;</code></example>
    public AuditFlushResult FlushResult { get; } = flushResult;
}
