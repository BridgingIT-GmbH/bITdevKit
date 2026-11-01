// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>A shared validated detail name that cannot replace reserved document identities.</summary>
/// <example><code>var name = new AuditDetailName("process");</code></example>
public readonly record struct AuditDetailName
{
    /// <summary>Validates length, Unicode, control characters and reserved names without echoing input.</summary>
    /// <example><code>var name = new AuditDetailName("process");</code></example>
    public AuditDetailName(string value) => this.Value = AuditStructuralValidation.DetailKey(value);

    /// <summary>The validated exact spelling.</summary>
    /// <example><code>var value = name.Value;</code></example>
    public string Value { get; }
}
