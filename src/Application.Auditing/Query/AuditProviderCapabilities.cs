// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Explicit mode-specific storage guarantees; capability metadata is not verified evidence.</summary>
/// <example><code>var value = new AuditProviderCapabilities();</code></example>
public sealed record AuditProviderCapabilities
{
    /// <summary>Provider identity.</summary>
    /// <example><code>var value = snapshot.Provider;</code></example>
    public string Provider { get; init; } = "Custom";

    /// <summary>Whether approved typed queries are implemented.</summary>
    /// <example><code>var value = snapshot.SupportsQueries;</code></example>
    public bool SupportsQueries { get; init; } = false;

    /// <summary>Whether committed windows and lifecycle/availability checks are implemented.</summary>
    /// <example><code>var value = snapshot.SupportsResumableExtraction;</code></example>
    public bool SupportsResumableExtraction { get; init; } = false;

    /// <summary>Whether confirmed persisted data survives process loss.</summary>
    /// <example><code>var value = snapshot.IsDurable;</code></example>
    public bool IsDurable { get; init; } = false;

    /// <summary>Whether independent processes coordinate one store.</summary>
    /// <example><code>var value = snapshot.IsShared;</code></example>
    public bool IsShared { get; init; } = false;

    /// <summary>Whether append acknowledges volatile queue acceptance.</summary>
    /// <example><code>var value = snapshot.Buffered;</code></example>
    public bool Buffered { get; init; } = false;

    /// <summary>Declared search visibility after append acknowledgment.</summary>
    /// <example><code>var value = snapshot.Visibility;</code></example>
    public string Visibility { get; init; } = "Unknown";

    /// <summary>Delivered retained-document readers.</summary>
    /// <example><code>var value = snapshot.ReadableSchemaVersions;</code></example>
    public ImmutableArray<int> ReadableSchemaVersions { get; init; } = [1];

    /// <summary>Actually supported relational backend names.</summary>
    /// <example><code>var value = snapshot.Backends;</code></example>
    public ImmutableArray<string> Backends { get; init; } = [];

    /// <summary>Whether the provider owns post-producer root coordination and its bounded writer drain.</summary>
    /// <example><code>var owned = provider.Capabilities.CoordinatesShutdown;</code></example>
    public bool CoordinatesShutdown { get; init; }
}
