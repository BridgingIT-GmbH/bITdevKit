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
        Func<TEntity, object> providerValueAccessor)
    {
        EnsureArg.IsNotNull(property, nameof(property));
        EnsureArg.IsNotNullOrEmpty(columnName, nameof(columnName));
        EnsureArg.IsNotNull(providerClrType, nameof(providerClrType));
        EnsureArg.IsNotNull(providerValueAccessor, nameof(providerValueAccessor));

        this.Property = property;
        this.ColumnName = columnName;
        this.ProviderClrType = providerClrType;
        this.ValueGenerated = valueGenerated;
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
