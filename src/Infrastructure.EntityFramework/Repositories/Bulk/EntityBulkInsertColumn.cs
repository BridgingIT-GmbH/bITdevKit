// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Describes one provider-neutral writable database column in an entity bulk insert batch.
/// </summary>
/// <typeparam name="TEntity">The entity type containing the column value.</typeparam>
/// <example>
/// <code>
/// var providerValue = column.GetProviderValue(person);
/// </code>
/// </example>
public sealed class EntityBulkInsertColumn<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityBulkInsertColumn{TEntity}"/> class.
    /// </summary>
    /// <param name="property">The EF property metadata mapped to the column.</param>
    /// <param name="columnName">The unquoted database column name.</param>
    /// <param name="providerClrType">The CLR type expected by the database provider after value conversion.</param>
    /// <param name="valueGenerated">The EF store-generated value behavior for the mapped property.</param>
    /// <param name="providerValueAccessor">An accessor that obtains and converts the entity value for the provider.</param>
    /// <example>
    /// <code>
    /// var column = new EntityBulkInsertColumn&lt;Person&gt;(
    ///     property,
    ///     "FirstName",
    ///     typeof(string),
    ///     ValueGenerated.Never,
    ///     person => person.FirstName);
    /// </code>
    /// </example>
    public EntityBulkInsertColumn(
        IProperty property,
        string columnName,
        Type providerClrType,
        ValueGenerated valueGenerated,
        Func<TEntity, object> providerValueAccessor
    )
        : this(
            property,
            columnName,
            providerClrType,
            valueGenerated,
            EntityBulkInsertColumnSource.ClrProperty,
            false,
            providerValueAccessor
        ) { }

    /// <summary>
    /// Initializes a column descriptor with its value source and identity classification.
    /// </summary>
    /// <param name="property">The EF property metadata mapped to the column.</param>
    /// <param name="columnName">The unquoted database column name.</param>
    /// <param name="providerClrType">The provider CLR type.</param>
    /// <param name="valueGenerated">The EF value-generation behavior.</param>
    /// <param name="source">The deterministic value source.</param>
    /// <param name="isIdentity">Whether this is an actual store identity column.</param>
    /// <param name="providerValueAccessor">The provider-value accessor.</param>
    /// <example>
    /// <code>
    /// var column = new EntityBulkInsertColumn&lt;Person&gt;(
    ///     property, "Discriminator", typeof(string), ValueGenerated.Never,
    ///     EntityBulkInsertColumnSource.MetadataConstant, false, _ => "Person");
    /// </code>
    /// </example>
    public EntityBulkInsertColumn(
        IProperty property,
        string columnName,
        Type providerClrType,
        ValueGenerated valueGenerated,
        EntityBulkInsertColumnSource source,
        bool isIdentity,
        Func<TEntity, object> providerValueAccessor
    )
    {
        EnsureArg.IsNotNull(property, nameof(property));
        EnsureArg.IsNotNullOrEmpty(columnName, nameof(columnName));
        EnsureArg.IsNotNull(providerClrType, nameof(providerClrType));
        EnsureArg.IsNotNull(providerValueAccessor, nameof(providerValueAccessor));

        this.Property = property;
        this.ColumnName = columnName;
        this.ProviderClrType = providerClrType;
        this.ValueGenerated = valueGenerated;
        this.Source = source;
        this.IsIdentity = isIdentity;
        this.ProviderValueAccessor = providerValueAccessor;
    }

    /// <summary>
    /// Gets the EF metadata for the property mapped to this column.
    /// </summary>
    /// <example>
    /// <code>
    /// var propertyName = column.Property.Name;
    /// </code>
    /// </example>
    public IProperty Property { get; }

    /// <summary>
    /// Gets the unquoted database column name.
    /// </summary>
    /// <example>
    /// <code>
    /// var columnName = column.ColumnName;
    /// </code>
    /// </example>
    public string ColumnName { get; }

    /// <summary>
    /// Gets the CLR type expected by the database provider after EF value conversion.
    /// </summary>
    /// <example>
    /// <code>
    /// var providerType = column.ProviderClrType;
    /// </code>
    /// </example>
    public Type ProviderClrType { get; }

    /// <summary>
    /// Gets the EF store-generated value behavior for the mapped property.
    /// </summary>
    /// <example>
    /// <code>
    /// var isGenerated = column.ValueGenerated is ValueGenerated.OnAdd;
    /// </code>
    /// </example>
    public ValueGenerated ValueGenerated { get; }

    /// <summary>Gets the deterministic source used to obtain this column's value.</summary>
    /// <example><code>var source = column.Source;</code></example>
    public EntityBulkInsertColumnSource Source { get; }

    /// <summary>Gets a value indicating whether this is an actual identity column.</summary>
    /// <example><code>var keepIdentity = column.IsIdentity;</code></example>
    public bool IsIdentity { get; }

    /// <summary>
    /// Gets the accessor that reads an entity value and converts it to the provider value when necessary.
    /// </summary>
    /// <example>
    /// <code>
    /// var providerValue = column.GetProviderValue(person);
    /// </code>
    /// </example>
    public Func<TEntity, object> ProviderValueAccessor { get; }

    /// <summary>
    /// Gets the converted provider value for the specified entity.
    /// </summary>
    /// <param name="entity">The entity that supplies the column value.</param>
    /// <returns>The value expected by the database provider, or <see langword="null"/> for a database null value.</returns>
    /// <example>
    /// <code>
    /// var providerValue = column.GetProviderValue(person);
    /// </code>
    /// </example>
    public object GetProviderValue(TEntity entity)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        return this.ProviderValueAccessor(entity);
    }
}
