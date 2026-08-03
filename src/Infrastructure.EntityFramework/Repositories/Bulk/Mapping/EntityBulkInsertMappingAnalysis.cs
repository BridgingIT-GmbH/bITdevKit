// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Represents side-effect-free EF model, graph, tracking, and value-source analysis for a bulk insert.
/// </summary>
/// <typeparam name="TEntity">The analyzed entity type.</typeparam>
/// <example>
/// <code>
/// var analysis = builder.Analyze(dbContext, entities, options);
/// var batch = builder.Build(analysis);
/// </code>
/// </example>
public sealed class EntityBulkInsertMappingAnalysis<TEntity>
    where TEntity : class, IEntity
{
    internal EntityBulkInsertMappingAnalysis(
        DbContext context,
        IEntityType entityType,
        string schema,
        string tableName,
        IReadOnlyList<TEntity> entities,
        IReadOnlyList<EntityBulkInsertPropertyMapping<TEntity>> mappings,
        EntityBulkInsertOptions options
    )
    {
        this.Context = context;
        this.EntityType = entityType;
        this.Schema = schema;
        this.TableName = tableName;
        this.Entities = entities;
        this.Mappings = mappings;
        this.Options = options;
    }

    /// <summary>Gets the analyzed root EF entity metadata.</summary>
    /// <example><code>var entityName = analysis.EntityType.Name;</code></example>
    public IEntityType EntityType { get; }

    /// <summary>Gets the target database schema, or <see langword="null"/>.</summary>
    /// <example><code>var schema = analysis.Schema;</code></example>
    public string Schema { get; }

    /// <summary>Gets the single target table.</summary>
    /// <example><code>var table = analysis.TableName;</code></example>
    public string TableName { get; }

    /// <summary>Gets the analyzed entities in insertion order.</summary>
    /// <example><code>var count = analysis.Entities.Count;</code></example>
    public IReadOnlyList<TEntity> Entities { get; }

    internal DbContext Context { get; }

    internal IReadOnlyList<EntityBulkInsertPropertyMapping<TEntity>> Mappings { get; }

    internal EntityBulkInsertOptions Options { get; }
}

internal sealed record EntityBulkInsertPropertyMapping<TEntity>(
    IProperty Property,
    string ColumnName,
    Type ProviderClrType,
    ValueGenerated ValueGenerated,
    EntityBulkInsertColumnSource Source,
    bool IsIdentity,
    bool IsRequired,
    Func<TEntity, object> ProviderValueAccessor
)
    where TEntity : class, IEntity;
