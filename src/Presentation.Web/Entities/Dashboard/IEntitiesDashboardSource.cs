// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using BridgingIT.DevKit.Common;

/// <summary>One way to read and write an entity type (repository or ActiveEntity).</summary>
internal interface IEntitiesDashboardSource
{
    /// <summary>Gets the source label (<c>Repository</c> or <c>ActiveEntity</c>).</summary>
    string SourceName { get; }

    /// <summary>Gets the entity type.</summary>
    Type EntityType { get; }

    /// <summary>Gets the id type.</summary>
    Type IdType { get; }

    /// <summary>Finds the entities that match the filter.</summary>
    Task<Result<IEnumerable<object>>> FindAllAsync(IServiceProvider services, FilterModel filter, CancellationToken cancellationToken);

    /// <summary>Finds one entity by id.</summary>
    Task<Result<object>> FindOneAsync(IServiceProvider services, object id, CancellationToken cancellationToken);

    /// <summary>Inserts a new entity.</summary>
    Task<Result<object>> InsertAsync(IServiceProvider services, object entity, CancellationToken cancellationToken);

    /// <summary>Updates an existing entity.</summary>
    Task<Result<object>> UpdateAsync(IServiceProvider services, object entity, CancellationToken cancellationToken);

    /// <summary>Deletes an entity by id.</summary>
    Task<Result> DeleteAsync(IServiceProvider services, object id, CancellationToken cancellationToken);
}
