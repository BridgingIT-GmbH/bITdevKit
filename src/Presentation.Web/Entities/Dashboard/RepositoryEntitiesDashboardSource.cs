// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using BridgingIT.DevKit.Common;

/// <summary>Reads and writes an entity through its registered <c>IGenericRepository&lt;T&gt;</c>.</summary>
/// <param name="entityType">The entity type.</param>
/// <param name="idType">The id type.</param>
internal sealed class RepositoryEntitiesDashboardSource(Type entityType, Type idType) : IEntitiesDashboardSource
{
    private const EntitiesDashboardSourceKind Kind = EntitiesDashboardSourceKind.Repository;

    public string SourceName => "Repository";

    public Type EntityType { get; } = entityType;

    public Type IdType { get; } = idType;

    public Task<Result<IEnumerable<object>>> FindAllAsync(IServiceProvider services, FilterModel filter, CancellationToken cancellationToken) =>
        EntitiesDashboardInvoker.FindAllAsync(Kind, this.EntityType, this.IdType, services, filter, cancellationToken);

    public Task<Result<object>> FindOneAsync(IServiceProvider services, object id, CancellationToken cancellationToken) =>
        EntitiesDashboardInvoker.FindOneAsync(Kind, this.EntityType, this.IdType, services, id, cancellationToken);

    public Task<Result<object>> InsertAsync(IServiceProvider services, object entity, CancellationToken cancellationToken) =>
        EntitiesDashboardInvoker.InsertAsync(Kind, this.EntityType, this.IdType, services, entity, cancellationToken);

    public Task<Result<object>> UpdateAsync(IServiceProvider services, object entity, CancellationToken cancellationToken) =>
        EntitiesDashboardInvoker.UpdateAsync(Kind, this.EntityType, this.IdType, services, entity, cancellationToken);

    public Task<Result> DeleteAsync(IServiceProvider services, object id, CancellationToken cancellationToken) =>
        EntitiesDashboardInvoker.DeleteAsync(Kind, this.EntityType, this.IdType, services, id, cancellationToken);
}
