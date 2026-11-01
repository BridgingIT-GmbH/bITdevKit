// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>A captured actor independent of a principal or live identity lookup.</summary>
/// <example><code>var value = new AuditActor();</code></example>
public sealed record AuditActor
{
    /// <summary>Anonymous, User, or explicitly configured Service attribution.</summary>
    /// <example><code>var value = snapshot.Kind;</code></example>
    public string Kind { get; init; } = "Anonymous";

    /// <summary>Whether authentication was observed during capture.</summary>
    /// <example><code>var value = snapshot.IsAuthenticated;</code></example>
    public bool IsAuthenticated { get; init; }

    /// <summary>The captured user identifier, when available.</summary>
    /// <example><code>var value = snapshot.UserId;</code></example>
    public string UserId { get; init; }

    /// <summary>The captured approved user display name.</summary>
    /// <example><code>var value = snapshot.DisplayName;</code></example>
    public string DisplayName { get; init; }

}
