// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Specifications;

public static class RepositoryExtensions
{
    public static async Task<IEnumerable<TID>> FindAllIdsAsync<TEntity, TID>(
        this IGenericReadOnlyRepository<TEntity> source,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return (await source.ProjectAllAsync(
            e => e.Id,
            options: options,
            cancellationToken: cancellationToken).AnyContext()).Select(i => i.To<TID>());
    }

    public static async Task<IEnumerable<TID>> FindAllIdsAsync<TEntity, TID>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return (await source.ProjectAllAsync(
            specification: specification,
            e => e.Id,
            options: options,
            cancellationToken: cancellationToken).AnyContext()).Select(i => i.To<TID>());
    }

    public static async Task<IEnumerable<TID>> FindAllIdsAsync<TEntity, TID>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return (await source.ProjectAllAsync(
            specifications: specifications,
            e => e.Id,
            options: options,
            cancellationToken: cancellationToken).AnyContext()).Select(i => i.To<TID>());
    }
}