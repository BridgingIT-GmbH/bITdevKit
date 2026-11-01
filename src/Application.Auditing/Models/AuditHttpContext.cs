// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Optional sanitized HTTP boundary metadata.</summary>
/// <example><code>var value = new AuditHttpContext();</code></example>
public sealed record AuditHttpContext
{
    /// <summary>Observed HTTP method.</summary>
    /// <example><code>var value = snapshot.Method;</code></example>
    public string Method { get; init; }

    /// <summary>Normalized route template.</summary>
    /// <example><code>var value = snapshot.Route;</code></example>
    public string Route { get; init; }

    /// <summary>Bounded sanitized path without a query string.</summary>
    /// <example><code>var value = snapshot.Path;</code></example>
    public string Path { get; init; }

    /// <summary>Final observed HTTP status.</summary>
    /// <example><code>var value = snapshot.Status;</code></example>
    public int? Status { get; init; }

    /// <summary>Approved endpoint display name.</summary>
    /// <example><code>var value = snapshot.Endpoint;</code></example>
    public string Endpoint { get; init; }

}
