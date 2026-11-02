// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

/// <summary>How a configured property is presented by the Entities dashboard.</summary>
internal enum EntitiesDashboardPropertyMode
{
    /// <summary>The value is replaced by the mask text.</summary>
    Mask,

    /// <summary>The property is omitted entirely.</summary>
    Hide
}

/// <summary>A masking or hiding rule for one property path.</summary>
/// <param name="Path">The property path (<c>Email</c> or <c>Address.Phone</c>).</param>
/// <param name="Mode">The rule mode.</param>
/// <param name="MaskText">The text shown for masked values.</param>
internal sealed record EntitiesDashboardPropertyRule(string Path, EntitiesDashboardPropertyMode Mode, string MaskText);

/// <summary>Collects the per-entity setup (rules, references, opt-ins) written by the entity builder.</summary>
internal sealed class EntitiesDashboardEntityConfiguration(Type entityType)
{
    /// <summary>Gets the configured entity type.</summary>
    public Type EntityType { get; } = entityType;

    /// <summary>Gets or sets a value indicating whether the entity is excluded from the dashboard.</summary>
    public bool Excluded { get; set; }

    /// <summary>Gets or sets a value indicating whether non-public setters and constructors may be used.</summary>
    public bool AllowNonPublicSetters { get; set; }

    /// <summary>Gets the masking and hiding rules by property path (last write wins).</summary>
    public Dictionary<string, EntitiesDashboardPropertyRule> Properties { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the reference overrides by property path. A null target suppresses detection.</summary>
    public Dictionary<string, Type> References { get; } = new(StringComparer.OrdinalIgnoreCase);
}
