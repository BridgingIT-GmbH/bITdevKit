// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

/// <summary>
/// An entity type the Entities dashboard can show. Immutable after the catalog publishes it, so it can be shared across requests.
/// Holds metadata only, never entity data.
/// </summary>
internal sealed class EntitiesDashboardEntity
{
    /// <summary>Gets the catalog key (the full type name).</summary>
    public string Key { get; init; }

    /// <summary>Gets the short type name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the shortened full name used for tooltips.</summary>
    public string DisplayName { get; init; }

    /// <summary>Gets the entity CLR type.</summary>
    public Type EntityType { get; init; }

    /// <summary>Gets the id CLR type (primitive or typed id).</summary>
    public Type IdType { get; init; }

    /// <summary>Gets the primitive type behind the id.</summary>
    public Type IdPrimitiveType { get; init; }

    /// <summary>Gets the available sources, repository first.</summary>
    public IReadOnlyList<IEntitiesDashboardSource> Sources { get; init; } = [];

    /// <summary>Gets the top-level property descriptors (Id first, concurrency and audit last).</summary>
    public IReadOnlyList<EntityPropertyDescriptor> Properties { get; init; } = [];

    /// <summary>Gets a value indicating whether new rows can be created.</summary>
    public bool CanCreate { get; init; }

    /// <summary>Gets a value indicating whether rows can be updated.</summary>
    public bool CanUpdate { get; init; }

    /// <summary>Gets a value indicating whether rows can be deleted.</summary>
    public bool CanDelete { get; init; } = true;

    /// <summary>Gets the text explaining why create or update is unavailable, or null.</summary>
    public string CapabilityNote { get; init; }

    /// <summary>Gets a value indicating whether the entity implements <c>IAuditable</c>.</summary>
    public bool IsAuditable { get; init; }

    /// <summary>Gets a value indicating whether the entity implements <c>IConcurrency</c>.</summary>
    public bool IsConcurrency { get; init; }

    /// <summary>Gets a value indicating whether the entity implements <c>ISoftDeletable</c>.</summary>
    public bool IsSoftDeletable { get; init; }

    /// <summary>Gets the factory for new instances, or null when the type cannot be created.</summary>
    public Func<object> Creator { get; init; }

    /// <summary>Gets the default source (the repository when available).</summary>
    public IEntitiesDashboardSource DefaultSource => this.Sources[0];

    /// <summary>Gets the id property descriptor.</summary>
    public EntityPropertyDescriptor IdProperty => this.Properties.FirstOrDefault(p => p.IsId);

    /// <summary>Finds a descriptor by path (<c>Name</c> or <c>Parent.Member</c>), ignoring case.</summary>
    /// <param name="path">The property path.</param>
    /// <returns>The descriptor, or null.</returns>
    public EntityPropertyDescriptor FindProperty(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = path.Split('.');
        var property = this.Properties.FirstOrDefault(p => string.Equals(p.Name, segments[0], StringComparison.OrdinalIgnoreCase));
        if (property == null || segments.Length == 1)
        {
            return property;
        }

        return segments.Length == 2
            ? property.Members.FirstOrDefault(m => string.Equals(m.Name, segments[1], StringComparison.OrdinalIgnoreCase))
            : null;
    }
}
