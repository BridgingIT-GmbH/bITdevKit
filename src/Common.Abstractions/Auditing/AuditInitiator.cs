// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>A copied permitted initiating identity, separate from the worker execution actor.</summary>
/// <example><code>var metadata = new AuditInitiator();</code></example>
public sealed record AuditInitiator
{
    /// <summary>Permitted initiating actor kind.</summary>
    /// <example><code>var value = metadata.Kind;</code></example>
    public string Kind { get; init; }

    /// <summary>Permitted opaque initiating user identity.</summary>
    /// <example><code>var value = metadata.UserId;</code></example>
    public string UserId { get; init; }

    /// <summary>Permitted initiating display label.</summary>
    /// <example><code>var value = metadata.DisplayName;</code></example>
    public string DisplayName { get; init; }

    /// <summary>Whether the initiating actor was authenticated at capture.</summary>
    /// <example><code>var value = metadata.IsAuthenticated;</code></example>
    public bool IsAuthenticated { get; init; }
}
