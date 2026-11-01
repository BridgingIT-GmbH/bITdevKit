// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>An optional whole-document authentication envelope. A stored envelope is not a verification result.</summary>
/// <example><code>var value = new AuditIntegrityProtection();</code></example>
public sealed record AuditIntegrityProtection
{
    /// <summary>Protection envelope version.</summary>
    /// <example><code>var value = snapshot.ProtectionVersion;</code></example>
    public int ProtectionVersion { get; init; } = 1;

    /// <summary>Pinned algorithm identity.</summary>
    /// <example><code>var value = snapshot.Algorithm;</code></example>
    public string Algorithm { get; init; } = "HMAC-SHA-256";

    /// <summary>External host key identifier, never the key.</summary>
    /// <example><code>var value = snapshot.KeyId;</code></example>
    public string KeyId { get; init; }

    /// <summary>Canonical input profile.</summary>
    /// <example><code>var value = snapshot.CanonicalizationVersion;</code></example>
    public int CanonicalizationVersion { get; init; } = 1;

    /// <summary>Full strict Base64 authentication tag.</summary>
    /// <example><code>var value = snapshot.Tag;</code></example>
    public string Tag { get; init; }

}
