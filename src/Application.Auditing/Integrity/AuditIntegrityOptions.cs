// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>Trusted integrity policy outside editable stored documents.</summary>
/// <example><code>builder.WithHmacSha256&lt;ExternalAuditKeys&gt;(new() { KeyResolutionTimeout = TimeSpan.FromSeconds(2) });</code></example>
public sealed record AuditIntegrityOptions
{
    /// <summary>Explicit host allowance for all retained unsigned history; such evidence is always NotProtected.</summary>
    /// <remarks>Default false. Record fields cannot select this allowance or disable required verification.</remarks>
    /// <example><code>var allowed = options.AllowUnsignedHistory;</code></example>
    public bool AllowUnsignedHistory { get; init; }

    /// <summary>Maximum time to resolve one external operation-owned key.</summary>
    /// <example><code>var timeout = options.KeyResolutionTimeout;</code></example>
    public TimeSpan KeyResolutionTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Rejects invalid local policy before host startup.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (this.KeyResolutionTimeout <= TimeSpan.Zero || this.KeyResolutionTimeout > TimeSpan.FromMinutes(1)) throw new ArgumentException("Audit integrity timeout is invalid.");
    }
}
