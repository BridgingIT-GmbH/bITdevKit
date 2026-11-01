// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>An explicitly permitted source property delta.</summary>
/// <example><code>var value = new AuditPropertyChange();</code></example>
public sealed record AuditPropertyChange
{
    /// <summary>Source property or collection path.</summary>
    /// <example><code>var value = snapshot.Path;</code></example>
    public string Path { get; init; }

    /// <summary>Source scalar, collection, or graph path classification.</summary>
    /// <example><code>var kind = property.PathKind;</code></example>
    public string PathKind { get; init; }

    /// <summary>Source value type token, retained without loading or resolving that type.</summary>
    /// <example><code>var type = property.ValueType;</code></example>
    public string ValueType { get; init; }

    /// <summary>Observed collection/property operation.</summary>
    /// <example><code>var value = snapshot.Operation;</code></example>
    public string Operation { get; init; }

    /// <summary>Approved collection item identity.</summary>
    /// <example><code>var value = snapshot.ItemIdentity;</code></example>
    public string ItemIdentity { get; init; }

    /// <summary>Permitted old value or a policy marker.</summary>
    /// <example><code>var value = snapshot.OldValue;</code></example>
    public AuditValue OldValue { get; init; }

    /// <summary>Permitted new value or a policy marker.</summary>
    /// <example><code>var value = snapshot.NewValue;</code></example>
    public AuditValue NewValue { get; init; }

}
