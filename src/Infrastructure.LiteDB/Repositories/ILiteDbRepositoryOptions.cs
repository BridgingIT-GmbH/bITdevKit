// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using Common.Options;
using Domain.Repositories;

/// <summary>
/// Defines operations for i lite db repository options.
/// </summary>
public interface ILiteDbRepositoryOptions : IRepositoryOptions, ILoggerOptions
{
    /// <summary>
    /// Gets or sets the db context.
    /// </summary>
    ILiteDbContext DbContext { get; set; }

    /// <summary>Gets or sets the shared scoped session used by registered repositories and transaction adapters.</summary>
    LiteDbDatabaseSession Session { get; set; }

    /// <summary>Gets or sets an explicit collection name, or null for the entity type name.</summary>
    string CollectionName { get; set; }

    /// <summary>Gets or sets the maximum materialized selection size.</summary>
    int MaxMaterializedRows { get; set; }

    /// <summary>Gets or sets whether mutations compare and advance entity concurrency versions.</summary>
    bool EnableOptimisticConcurrency { get; set; }
}
