// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>A safe reason identifier and bounded aggregate count.</summary>
/// <example><code>var value = new AuditCaptureNote();</code></example>
public sealed record AuditCaptureNote
{
    /// <summary>Stable capture/delivery reason, without supplied payloads.</summary>
    /// <example><code>var value = snapshot.Code;</code></example>
    public string Code { get; init; } = "Unknown";

    /// <summary>Number of matching omitted or affected observations.</summary>
    /// <example><code>var value = snapshot.Count;</code></example>
    public long Count { get; init; } = 1;

}
