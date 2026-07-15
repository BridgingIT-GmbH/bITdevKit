// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Represents provider-neutral Entity Framework metadata and values prepared for one entity bulk insert operation.
/// </summary>
/// <typeparam name="TEntity">The entity type represented by the batch.</typeparam>
/// <example>
/// <code>
/// var batch = new EntityBulkInsertBatch&lt;Person&gt;(
///     entityType,
///     schema,
///     tableName,
///     people,
///     columns,
///     options);
/// </code>
/// </example>
public sealed class EntityBulkInsertBatch<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityBulkInsertBatch{TEntity}"/> class.
    /// </summary>
    /// <param name="entityType">The EF entity metadata for the batch root type.</param>
    /// <param name="schema">The unquoted database schema, or <see langword="null"/> when the table has no schema.</param>
    /// <param name="tableName">The unquoted database table name.</param>
    /// <param name="entities">The entities to insert in batch order.</param>
    /// <param name="columns">The ordered writable column descriptors.</param>
    /// <param name="options">The validated provider-neutral bulk insert options.</param>
    /// <example>
    /// <code>
    /// var batch = new EntityBulkInsertBatch&lt;Person&gt;(
    ///     entityType,
    ///     "dbo",
    ///     "Persons",
    ///     people,
    ///     columns,
    ///     options);
    /// </code>
    /// </example>
    public EntityBulkInsertBatch(
        IEntityType entityType,
        string schema,
        string tableName,
        IReadOnlyList<TEntity> entities,
        IReadOnlyList<EntityBulkInsertColumn<TEntity>> columns,
        EntityBulkInsertOptions options)
    {
        EnsureArg.IsNotNull(entityType, nameof(entityType));
        EnsureArg.IsNotNullOrEmpty(tableName, nameof(tableName));
        EnsureArg.IsNotNull(entities, nameof(entities));
        EnsureArg.IsNotNull(columns, nameof(columns));
        EnsureArg.IsNotNull(options, nameof(options));

        this.EntityType = entityType;
        this.Schema = schema;
        this.TableName = tableName;
        this.Entities = entities;
        this.Columns = columns;
        this.Options = options;
    }

    /// <summary>
    /// Gets the EF metadata for the entity type mapped to the target table.
    /// </summary>
    /// <example>
    /// <code>
    /// var entityName = batch.EntityType.Name;
    /// </code>
    /// </example>
    public IEntityType EntityType { get; }

    /// <summary>
    /// Gets the unquoted target schema, or <see langword="null"/> when the target table has no schema.
    /// </summary>
    /// <example>
    /// <code>
    /// var schema = batch.Schema ?? "default";
    /// </code>
    /// </example>
    public string Schema { get; }

    /// <summary>
    /// Gets the unquoted target table name.
    /// </summary>
    /// <example>
    /// <code>
    /// var tableName = batch.TableName;
    /// </code>
    /// </example>
    public string TableName { get; }

    /// <summary>
    /// Gets the entities to insert in their prepared batch order.
    /// </summary>
    /// <example>
    /// <code>
    /// var entityCount = batch.Entities.Count;
    /// </code>
    /// </example>
    public IReadOnlyList<TEntity> Entities { get; }

    /// <summary>
    /// Gets the ordered writable column descriptors for every entity row.
    /// </summary>
    /// <example>
    /// <code>
    /// var firstColumn = batch.Columns[0];
    /// </code>
    /// </example>
    public IReadOnlyList<EntityBulkInsertColumn<TEntity>> Columns { get; }

    /// <summary>
    /// Gets the validated provider-neutral options for this operation.
    /// </summary>
    /// <example>
    /// <code>
    /// var batchSize = batch.Options.BatchSize;
    /// </code>
    /// </example>
    public EntityBulkInsertOptions Options { get; }
}
