// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.Specifications;
using Microsoft.Extensions.DependencyInjection;

/// <summary>The data access path an entity is read from and written through.</summary>
internal enum EntitiesDashboardSourceKind
{
    /// <summary>The registered <c>IGenericRepository&lt;T&gt;</c>.</summary>
    Repository,

    /// <summary>The ActiveEntity static and instance APIs.</summary>
    ActiveEntity
}

/// <summary>
/// Calls the closed generic repository result extensions and ActiveEntity APIs through compiled delegates,
/// cached per source kind, entity type and id type. Results are returned as object-typed results.
/// </summary>
internal static class EntitiesDashboardInvoker
{
    private static readonly ConcurrentDictionary<(EntitiesDashboardSourceKind, Type, Type), Delegates> Cache = new();

    /// <summary>Finds entities that match the filter.</summary>
    public static Task<Result<IEnumerable<object>>> FindAllAsync(
        EntitiesDashboardSourceKind kind, Type entityType, Type idType, IServiceProvider services, FilterModel filter, CancellationToken cancellationToken) =>
        Get(kind, entityType, idType).FindAll(services, filter, cancellationToken);

    /// <summary>Finds one entity by id.</summary>
    public static Task<Result<object>> FindOneAsync(
        EntitiesDashboardSourceKind kind, Type entityType, Type idType, IServiceProvider services, object id, CancellationToken cancellationToken) =>
        Get(kind, entityType, idType).FindOne(services, id, cancellationToken);

    /// <summary>Inserts a new entity.</summary>
    public static Task<Result<object>> InsertAsync(
        EntitiesDashboardSourceKind kind, Type entityType, Type idType, IServiceProvider services, object entity, CancellationToken cancellationToken) =>
        Get(kind, entityType, idType).Insert(services, entity, cancellationToken);

    /// <summary>Updates an existing entity.</summary>
    public static Task<Result<object>> UpdateAsync(
        EntitiesDashboardSourceKind kind, Type entityType, Type idType, IServiceProvider services, object entity, CancellationToken cancellationToken) =>
        Get(kind, entityType, idType).Update(services, entity, cancellationToken);

    /// <summary>Deletes an entity by id.</summary>
    public static Task<Result> DeleteAsync(
        EntitiesDashboardSourceKind kind, Type entityType, Type idType, IServiceProvider services, object id, CancellationToken cancellationToken) =>
        Get(kind, entityType, idType).Delete(services, id, cancellationToken);

    private static Delegates Get(EntitiesDashboardSourceKind kind, Type entityType, Type idType) =>
        Cache.GetOrAdd((kind, entityType, idType), key => key.Item1 == EntitiesDashboardSourceKind.Repository
            ? BuildRepository(key.Item2)
            : BuildActiveEntity(key.Item2, key.Item3));

    private static Delegates BuildRepository(Type entityType)
    {
        var repositoryType = typeof(IGenericRepository<>).MakeGenericType(entityType);
        var sp = Expression.Parameter(typeof(IServiceProvider), "services");
        var ct = Expression.Parameter(typeof(CancellationToken), "ct");
        var filter = Expression.Parameter(typeof(FilterModel), "filter");
        var id = Expression.Parameter(typeof(object), "id");
        var entity = Expression.Parameter(typeof(object), "entity");
        var repository = Expression.Call(GetRequiredServiceMethod.MakeGenericMethod(repositoryType), sp);

        var specifications = Expression.Constant(null, typeof(IEnumerable<>).MakeGenericType(typeof(ISpecification<>).MakeGenericType(entityType)));
        var options = Expression.Constant(null, typeof(IFindOptions<>).MakeGenericType(entityType));
        var typedEntity = Expression.Convert(entity, entityType);

        var findAll = Expression.Call(
            Extension(typeof(GenericReadOnlyRepositoryResultExtensions), "FindAllResultAsync", entityType, typeof(FilterModel)),
            repository, filter, specifications, ct);
        var findOne = Expression.Call(
            Extension(typeof(GenericReadOnlyRepositoryResultExtensions), "FindOneResultAsync", entityType, typeof(object)),
            repository, id, options, ct);
        var insert = Expression.Call(
            Extension(typeof(GenericRepositoryResultExtensions), "InsertResultAsync", entityType, null), repository, typedEntity, ct);
        var update = Expression.Call(
            Extension(typeof(GenericRepositoryResultExtensions), "UpdateResultAsync", entityType, null), repository, typedEntity, ct);
        var delete = Expression.Call(
            Extension(typeof(GenericRepositoryResultExtensions), "DeleteResultAsync", entityType, null), repository, id, ct);

        return new Delegates(
            Expression.Lambda<Func<IServiceProvider, FilterModel, CancellationToken, Task<Result<IEnumerable<object>>>>>(
                Helper(nameof(ToObjects), entityType, findAll), sp, filter, ct).Compile(),
            Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<Result<object>>>>(
                Helper(nameof(ToObject), entityType, findOne), sp, id, ct).Compile(),
            Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<Result<object>>>>(
                Helper(nameof(ToObject), entityType, insert), sp, entity, ct).Compile(),
            Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<Result<object>>>>(
                Helper(nameof(ToObject), entityType, update), sp, entity, ct).Compile(),
            Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<Result>>>(
                Helper(nameof(ToResult), typeof(RepositoryActionResult), delete), sp, id, ct).Compile());
    }

    private static Delegates BuildActiveEntity(Type entityType, Type idType)
    {
        var closed = typeof(ActiveEntity<,>).MakeGenericType(entityType, idType);
        var sp = Expression.Parameter(typeof(IServiceProvider), "services");
        var ct = Expression.Parameter(typeof(CancellationToken), "ct");
        var filter = Expression.Parameter(typeof(FilterModel), "filter");
        var id = Expression.Parameter(typeof(object), "id");
        var entity = Expression.Parameter(typeof(object), "entity");

        var specificationsType = typeof(IEnumerable<>).MakeGenericType(typeof(ISpecification<>).MakeGenericType(entityType));
        var optionsType = typeof(IFindOptions<>).MakeGenericType(entityType);
        var typedEntity = Expression.Convert(entity, entityType);

        var findAll = Expression.Call(
            Static(closed, "FindAllAsync", typeof(FilterModel), specificationsType, typeof(CancellationToken)),
            filter, Expression.Constant(null, specificationsType), ct);
        var findOne = Expression.Call(
            Static(closed, "FindOneAsync", typeof(object), optionsType, typeof(CancellationToken)),
            id, Expression.Constant(null, optionsType), ct);
        var insert = Expression.Call(
            Static(closed, "InsertAsync", entityType, typeof(CancellationToken)), typedEntity, ct);
        var update = Expression.Call(
            typedEntity,
            closed.GetMethod("UpdateAsync", BindingFlags.Public | BindingFlags.Instance, null, [typeof(CancellationToken)], null)
                ?? throw new InvalidOperationException($"UpdateAsync(CancellationToken) was not found on {closed.Name}."),
            ct);
        var delete = Expression.Call(
            Static(closed, "DeleteAsync", typeof(object), typeof(CancellationToken)), id, ct);

        return new Delegates(
            Expression.Lambda<Func<IServiceProvider, FilterModel, CancellationToken, Task<Result<IEnumerable<object>>>>>(
                Helper(nameof(ToObjects), entityType, findAll), sp, filter, ct).Compile(),
            Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<Result<object>>>>(
                Helper(nameof(ToObject), entityType, findOne), sp, id, ct).Compile(),
            Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<Result<object>>>>(
                Helper(nameof(ToObject), entityType, insert), sp, entity, ct).Compile(),
            Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<Result<object>>>>(
                Helper(nameof(ToObject), entityType, update), sp, entity, ct).Compile(),
            Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task<Result>>>(delete, sp, id, ct).Compile());
    }

    private static readonly MethodInfo GetRequiredServiceMethod = typeof(ServiceProviderServiceExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(ServiceProviderServiceExtensions.GetRequiredService)
            && m.IsGenericMethodDefinition
            && m.GetParameters().Length == 1
            && m.GetParameters()[0].ParameterType == typeof(IServiceProvider));

    private static MethodInfo Extension(Type host, string name, Type entityType, Type secondParameter)
    {
        var method = host
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == name && m.IsGenericMethodDefinition)
            .FirstOrDefault(m => secondParameter == null || m.GetParameters()[1].ParameterType == secondParameter);

        return method?.MakeGenericMethod(entityType)
            ?? throw new InvalidOperationException($"{host.Name}.{name} was not found for {entityType.Name}.");
    }

    private static MethodInfo Static(Type closed, string name, params Type[] parameterTypes) =>
        closed.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy, null, parameterTypes, null)
            ?? throw new InvalidOperationException($"{name} with the expected parameters was not found on {closed.Name}.");

    private static Expression Helper(string name, Type typeArgument, Expression call) =>
        Expression.Call(typeof(EntitiesDashboardInvoker).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(typeArgument), call);

    private static async Task<Result<object>> ToObject<T>(Task<Result<T>> task)
    {
        var result = await task.ConfigureAwait(false);

        return result.Wrap<object>(result.IsSuccess ? result.Value : null);
    }

    private static async Task<Result<IEnumerable<object>>> ToObjects<T>(Task<Result<IEnumerable<T>>> task)
    {
        var result = await task.ConfigureAwait(false);
        IEnumerable<object> items = result.IsSuccess && result.Value != null
            ? result.Value.Cast<object>().ToList()
            : [];

        return result.Wrap(items);
    }

    private static async Task<Result> ToResult<T>(Task<Result<T>> task)
    {
        Result result = await task.ConfigureAwait(false);

        return result;
    }

    private sealed record Delegates(
        Func<IServiceProvider, FilterModel, CancellationToken, Task<Result<IEnumerable<object>>>> FindAll,
        Func<IServiceProvider, object, CancellationToken, Task<Result<object>>> FindOne,
        Func<IServiceProvider, object, CancellationToken, Task<Result<object>>> Insert,
        Func<IServiceProvider, object, CancellationToken, Task<Result<object>>> Update,
        Func<IServiceProvider, object, CancellationToken, Task<Result>> Delete);
}
