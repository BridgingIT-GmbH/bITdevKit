// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Configures LiteDB persistence for one ActiveEntity type.</summary>
/// <typeparam name="TEntity">The stored entity type.</typeparam>
/// <example><code>configuration.For&lt;LedgerEntry, Guid&gt;().UseLiteDbProvider(o => o.CollectionName = "ledger");</code></example>
public sealed class LiteDbActiveEntityProviderOptions<TEntity> : OptionsBase
    where TEntity : class, IEntity
{
    /// <summary>Gets or sets the named database registration.</summary>
    public string DatabaseName { get; set; } = "default";

    /// <summary>Gets or sets the collection name. Null selects a stable name derived from the entity type.</summary>
    public string CollectionName { get; set; }

    /// <summary>Gets or sets an optional custom identifier generator.</summary>
    public IEntityIdGenerator<TEntity> IdGenerator { get; set; }

    /// <summary>Gets or sets whether writes check concurrency versions.</summary>
    public bool EnableOptimisticConcurrency { get; set; } = true;

    /// <summary>Gets or sets the generator for new concurrency versions.</summary>
    public Func<Guid> VersionGenerator { get; set; } = GuidGenerator.CreateSequential;

    /// <summary>Gets or sets the maximum number of rows materialized by a query or set operation.</summary>
    public int MaxMaterializedRows { get; set; } = 10000;
}
