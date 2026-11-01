// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Safe static selection metadata; it is not an observed business event.</summary>
/// <example><code>var registration = new AuditBoundaryRegistration("Requester", "dispatch", "selected", "CustomerRename");</code></example>
public sealed record AuditBoundaryRegistration
{
    /// <summary>Validates a bounded source registration without resolving handlers.</summary>
    /// <example><code>var registration = new AuditBoundaryRegistration("Jobs", "execution", "unknown");</code></example>
    public AuditBoundaryRegistration(string feature, string boundary, string selection, string typeAlias = null)
    {
        this.Feature = AuditStructuralValidation.Required(feature, 128, nameof(feature));
        this.Boundary = AuditStructuralValidation.Required(boundary, 128, nameof(boundary));
        this.Selection = selection?.ToLowerInvariant() is "selected" or "omitted" or "disabled" or "unknown" ? selection.ToLowerInvariant() : throw new ArgumentException("Unknown audit selection state.", nameof(selection));
        this.TypeAlias = typeAlias is null ? null : AuditStructuralValidation.Required(typeAlias, 512, nameof(typeAlias));
    }

    /// <summary>The owning source feature.</summary>
    /// <example><code>var feature = registration.Feature;</code></example>
    public string Feature { get; }

    /// <summary>The source-owned boundary.</summary>
    /// <example><code>var boundary = registration.Boundary;</code></example>
    public string Boundary { get; }

    /// <summary>Declared selection; dynamic decisions remain unknown.</summary>
    /// <example><code>var selection = registration.Selection;</code></example>
    public string Selection { get; }

    /// <summary>An optional safe registered type identity.</summary>
    /// <example><code>var alias = registration.TypeAlias;</code></example>
    public string TypeAlias { get; }

    /// <summary>A deterministic registration identity for idempotent inventory.</summary>
    /// <example><code>var identity = registration.Identity;</code></example>
    public string Identity => this.Feature + ":" + this.Boundary + ":" + this.TypeAlias;
}
