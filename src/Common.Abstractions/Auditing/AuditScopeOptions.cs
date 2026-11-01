// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>An immutable scope naming and ownership snapshot.</summary>
/// <example><code>var options = AuditScopeOptionsBuilder.Create&lt;Customer&gt;("Rename").Build();</code></example>
public sealed record AuditScopeOptions
{
    /// <summary>Validates shared builder/type defaults without starting capture.</summary>
    /// <example><code>var options = new AuditScopeOptions(new AuditDescriptor("customers.rename"));</code></example>
    public AuditScopeOptions(AuditDescriptor descriptor, string kind = "Business", AuditScopeMode mode = AuditScopeMode.JoinOrStart)
    {
        this.Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        this.Kind = AuditStructuralValidation.Required(kind, 128, nameof(kind));
        this.Mode = Enum.IsDefined(mode) ? mode : throw new ArgumentOutOfRangeException(nameof(mode));
    }

    /// <summary>The copied business descriptor.</summary>
    /// <example><code>var key = options.Descriptor.Key;</code></example>
    public AuditDescriptor Descriptor { get; }

    /// <summary>The business or source-specific action kind.</summary>
    /// <example><code>var kind = options.Kind;</code></example>
    public string Kind { get; }

    /// <summary>The scope ownership choice.</summary>
    /// <example><code>var independent = options.Mode == AuditScopeMode.RequiresNew;</code></example>
    public AuditScopeMode Mode { get; }
}
