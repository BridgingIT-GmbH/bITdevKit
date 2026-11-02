// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

/// <summary>The kind of editor and value handling the dashboard uses for a property.</summary>
internal enum EntitiesDashboardEditorKind
{
    /// <summary>Single-line text.</summary>
    Text,

    /// <summary>Numeric input.</summary>
    Number,

    /// <summary>Guid input.</summary>
    Guid,

    /// <summary>Boolean toggle.</summary>
    Boolean,

    /// <summary>Date and time input.</summary>
    DateTime,

    /// <summary>CLR enum selection.</summary>
    Enum,

    /// <summary>Enumeration value object selection.</summary>
    Enumeration,

    /// <summary>JSON text for value objects and collections of scalars.</summary>
    Json,

    /// <summary>Not editable or displayable as a value (navigation properties, binary data).</summary>
    Unsupported
}

/// <summary>A link from a property to the entity it references.</summary>
/// <param name="TargetKey">The catalog key of the referenced entity.</param>
internal sealed record EntitiesDashboardReference(string TargetKey);

/// <summary>Immutable-after-discovery description of one entity property (or one level of a value-object member).</summary>
internal sealed class EntityPropertyDescriptor
{
    /// <summary>Gets the property path (<c>Name</c> or <c>Parent.Member</c>).</summary>
    public string Path { get; init; }

    /// <summary>Gets the property name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the declared CLR type.</summary>
    public Type ClrType { get; init; }

    /// <summary>Gets the CLR type without its nullable wrapper.</summary>
    public Type UnderlyingType { get; init; }

    /// <summary>Gets the editor kind.</summary>
    public EntitiesDashboardEditorKind EditorKind { get; init; }

    /// <summary>Gets a value indicating whether the property accepts null.</summary>
    public bool IsNullable { get; init; }

    /// <summary>Gets a value indicating whether the property cannot be edited.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>Gets the reason the property is read-only, if any.</summary>
    public string ReadOnlyReason { get; init; }

    /// <summary>Gets a value indicating whether the value is replaced by <see cref="MaskText"/>.</summary>
    public bool IsMasked { get; init; }

    /// <summary>Gets the mask text for masked properties.</summary>
    public string MaskText { get; init; }

    /// <summary>Gets a value indicating whether the property is removed from columns, forms and payloads.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Gets a value indicating whether the property can be used in filters.</summary>
    public bool IsFilterable { get; init; }

    /// <summary>Gets a value indicating whether the property can be sorted.</summary>
    public bool IsSortable { get; init; }

    /// <summary>Gets a value indicating whether the property belongs to the audit state.</summary>
    public bool IsAudit { get; init; }

    /// <summary>Gets a value indicating whether the property is the entity id.</summary>
    public bool IsId { get; init; }

    /// <summary>Gets a value indicating whether the property is a typed id (<c>EntityId&lt;T&gt;</c>).</summary>
    public bool IsTypedId { get; init; }

    /// <summary>Gets a value indicating whether the property is the concurrency version.</summary>
    public bool IsConcurrency { get; init; }

    /// <summary>Gets the one-level members of a value-object property.</summary>
    public IReadOnlyList<EntityPropertyDescriptor> Members { get; init; } = [];

    /// <summary>Gets the compiled getter (takes the owning instance).</summary>
    public Func<object, object> Getter { get; init; }

    /// <summary>Gets the compiled setter (takes the owning instance and the value), or null when not writable.</summary>
    public Action<object, object> Setter { get; init; }

    /// <summary>Gets the reference to another entity, set by the reference resolver before the catalog publishes the entity.</summary>
    public EntitiesDashboardReference Reference { get; internal set; }

    /// <summary>Gets a value indicating whether the property is shown (not hidden); masked properties are shown with their mask text.</summary>
    public bool IsVisible => !this.IsHidden;

    /// <summary>Gets a value indicating whether the property can be written through its setter.</summary>
    public bool IsWritable => !this.IsReadOnly && this.Setter != null;
}
