// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Linq.Expressions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;

/// <summary>Persists ActiveEntity records directly through a shared LiteDB database and scoped transaction session.</summary>
/// <typeparam name="TEntity">The stored entity type.</typeparam>
/// <typeparam name="TId">The entity identifier type.</typeparam>
/// <remarks>Use ActiveEntity APIs to run registered behaviors. This provider invokes supplied callbacks and owns persistence only. Dispose the DI scope asynchronously.</remarks>
/// <example><code>configuration.For&lt;LedgerEntry, Guid&gt;().UseLiteDbProvider();</code></example>
public sealed partial class LiteDbActiveEntityProvider<TEntity, TId> : IActiveEntityEntityProvider<TEntity, TId>
    where TEntity : class, IEntity
{
    private readonly LiteDbDatabaseSession session;
    private readonly LiteDbActiveEntityProviderOptions<TEntity> settings;
    private readonly LiteDbActiveEntityQuery<TEntity> query;
    private readonly ILogger logger;

    /// <summary>Creates an entity provider using an existing scoped database session and configured mappings.</summary>
    /// <param name="session">The scoped session, shared by entity providers selecting the same named database.</param>
    /// <param name="settings">Provider settings with an explicit collection name. The caller must keep them unchanged during use.</param>
    /// <param name="logger">Optional structured diagnostic logger.</param>
    /// <remarks>Prefer UseLiteDbProvider for registration, mapping setup, and options snapshots. Manual composition must configure the database mapper before first use.</remarks>
    public LiteDbActiveEntityProvider(LiteDbDatabaseSession session, LiteDbActiveEntityProviderOptions<TEntity> settings,
        ILogger<LiteDbActiveEntityProvider<TEntity, TId>> logger = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.CollectionName);
        ArgumentNullException.ThrowIfNull(settings.VersionGenerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MaxMaterializedRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(settings.MaxMaterializedRows, int.MaxValue);
        this.session = session;
        this.settings = settings;
        this.query = new LiteDbActiveEntityQuery<TEntity>(settings);
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<TEntity>> InsertAsync(TEntity entity, ActiveEntityCallbackOptions<TEntity, TId> callbacks = null, CancellationToken cancellationToken = default) =>
        this.WriteEntityAsync(entity, callbacks, WriteMode.Insert, cancellationToken);

    /// <inheritdoc />
    public Task<Result<TEntity>> UpdateAsync(TEntity entity, ActiveEntityCallbackOptions<TEntity, TId> callbacks = null, CancellationToken cancellationToken = default) =>
        this.WriteEntityAsync(entity, callbacks, WriteMode.Update, cancellationToken);

    /// <inheritdoc />
    public Task<Result<(TEntity entity, RepositoryActionResult action)>> UpsertAsync(TEntity entity, ActiveEntityCallbackOptions<TEntity, TId> callbacks = null, CancellationToken cancellationToken = default) =>
        this.UpsertCoreAsync(entity, callbacks, WriteMode.Upsert, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(TEntity entity, ActiveEntityCallbackOptions<TEntity, TId> callbacks = null, CancellationToken cancellationToken = default)
    {
        var result = await this.GuardAsync(async () => await this.session.ExecuteAsync(async () =>
        {
            if (entity is null) { return Result<bool>.Failure(new InvalidInputError("An entity is required.")); }

            await this.CallbackAsync(callbacks?.BeforeDeleteAsync, cancellationToken).ConfigureAwait(false);
            var before = await this.session.NativeAsync(db =>
            {
                var id = LiteDbActiveEntityMapping<TId>.ToBson(entity.Id);
                var collection = db.GetCollection<BsonDocument>(this.settings.CollectionName);
                var stored = collection.FindById(id) ?? throw new LiteDbProviderException(new NotFoundError());
                this.CheckConcurrency(entity, stored);
                if (!collection.Delete(id)) { throw new LiteDbProviderException(new NotFoundError()); }

                return stored;
            }, cancellationToken).ConfigureAwait(false);
            await this.CallbackAsync(callbacks?.AfterDeleteAsync, cancellationToken).ConfigureAwait(false);
            await this.session.NativeAsync(db =>
            {
                this.session.CaptureMutation(db, entity, before, null, this.settings.CollectionName, "Delete", "ActiveEntity");
                return true;
            }, cancellationToken).ConfigureAwait(false);
            return Result<bool>.Success(true);
        }, true, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors).WithMessages(result.Messages);
    }

    /// <inheritdoc />
    public Task<Result<TEntity>> FindOneAsync(object id, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.GuardAsync(() => this.session.ExecuteAsync(async () =>
        {
            var value = await this.session.NativeAsync(db =>
            {
                var key = LiteDbActiveEntityMapping<TId>.ToBson(id);
                return this.query.Build(db, [], options).Where(BsonExpression.Create("$._id = @0", key)).FirstOrDefault();
            }, cancellationToken).ConfigureAwait(false);
            return value is null ? Result<TEntity>.Failure(new NotFoundError()) : Result<TEntity>.Success(value);
        }, false, cancellationToken));

    /// <inheritdoc />
    public Task<Result<TEntity>> FindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindOneAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<TEntity>> FindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.GuardAsync(() => this.session.ExecuteAsync(async () =>
        {
            var selection = await this.session.NativeAsync(db => this.query.Select(db, specifications, FirstOptions(options), false, cancellationToken), cancellationToken).ConfigureAwait(false);
            var value = selection.Items.FirstOrDefault();
            return value is null ? Result<TEntity>.Failure(new NotFoundError()) : Result<TEntity>.Success(value);
        }, false, cancellationToken));

    /// <inheritdoc />
    public Task<Result<IEnumerable<TEntity>>> FindAllAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllAsync([], options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IEnumerable<TEntity>>> FindAllAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IEnumerable<TEntity>>> FindAllAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllAsync(specifications, entity => entity, options, cancellationToken);

    /// <inheritdoc />
    public Task<ResultPaged<TEntity>> FindAllPagedAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllPagedAsync([], options, cancellationToken);

    /// <inheritdoc />
    public Task<ResultPaged<TEntity>> FindAllPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllPagedAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public Task<ResultPaged<TEntity>> FindAllPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllPagedAsync(specifications, entity => entity, options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllAsync([], projection, options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllAsync([specification], projection, options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.GuardAsync(() => this.session.ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(projection);
            var selection = await this.session.NativeAsync(db => this.query.Select(db, specifications, options, false, cancellationToken), cancellationToken).ConfigureAwait(false);
            return Result<IEnumerable<TProjection>>.Success(selection.Items.Select(projection.Compile()).ToArray());
        }, false, cancellationToken));

    /// <inheritdoc />
    public Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllPagedAsync([], projection, options, cancellationToken);

    /// <inheritdoc />
    public Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllPagedAsync([specification], projection, options, cancellationToken);

    /// <inheritdoc />
    public async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var result = await this.GuardAsync(() => this.session.ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(projection);
            var selection = await this.session.NativeAsync(db => this.query.Select(db, specifications, options, true, cancellationToken), cancellationToken).ConfigureAwait(false);
            return Result<(IEnumerable<TProjection> Items, long Total, int Page, int PageSize)>.Success(
                (selection.Items.Select(projection.Compile()).ToArray(), selection.Total, selection.Page, selection.PageSize));
        }, false, cancellationToken)).ConfigureAwait(false);
        return result.IsSuccess
            ? ResultPaged<TProjection>.Success(result.Value.Items, result.Value.Total, result.Value.Page, result.Value.PageSize)
            : ResultPaged<TProjection>.Failure().WithErrors(result.Errors).WithMessages(result.Messages);
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(object id, CancellationToken cancellationToken = default) =>
        this.ReadAsync(db => db.GetCollection<BsonDocument>(this.settings.CollectionName).FindById(LiteDbActiveEntityMapping<TId>.ToBson(id)) is not null, cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(CancellationToken cancellationToken = default) =>
        this.ExistsAsync([], cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ExistsAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ReadAsync(db => this.query.Count(db, specifications, options, cancellationToken) > 0, cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> CountAsync(CancellationToken cancellationToken = default) => this.CountAsync([], cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> CountAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.CountAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> CountAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ReadAsync(db => this.query.Count(db, specifications, options, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<IEnumerable<TId>>> FindAllIdsAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllIdsAsync([], options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IEnumerable<TId>>> FindAllIdsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllIdsAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IEnumerable<TId>>> FindAllIdsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllAsync(specifications, entity => (TId)entity.Id, options, cancellationToken);

    /// <inheritdoc />
    public Task<ResultPaged<TId>> FindAllIdsPagedAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllIdsPagedAsync([], options, cancellationToken);

    /// <inheritdoc />
    public Task<ResultPaged<TId>> FindAllIdsPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllIdsPagedAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public Task<ResultPaged<TId>> FindAllIdsPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllPagedAsync(specifications, entity => (TId)entity.Id, options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IDatabaseTransaction>> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        this.GuardAsync(async () => Result<IDatabaseTransaction>.Success(await this.session.BeginAsync(cancellationToken).ConfigureAwait(false)));

    /// <inheritdoc />
    public Task<Result> CommitTransactionAsync(CancellationToken cancellationToken = default) => this.CompleteAsync(true, cancellationToken);

    /// <inheritdoc />
    public Task<Result> RollbackAsync(CancellationToken cancellationToken = default) => this.CompleteAsync(false, cancellationToken);

    private Task<Result<T>> ReadAsync<T>(Func<LiteDatabase, T> action, CancellationToken cancellationToken) =>
        this.GuardAsync(() => this.session.ExecuteAsync(async () => Result<T>.Success(
            await this.session.NativeAsync(action, cancellationToken).ConfigureAwait(false)), false, cancellationToken));

    private async Task<Result> CompleteAsync(bool commit, CancellationToken cancellationToken)
    {
        var result = await this.GuardAsync(async () =>
        {
            await this.session.CompleteAsync(commit, cancellationToken).ConfigureAwait(false);
            return Result<bool>.Success(true);
        }).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors).WithMessages(result.Messages);
    }

    private async Task<Result<T>> GuardAsync<T>(Func<Task<Result<T>>> action)
    {
        try { return await action().ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (LiteDbProviderException exception) { return Result<T>.Failure().WithErrors(exception.Errors).WithMessages(exception.Messages); }
        catch (Exception exception) when (!exception.IsTransientException())
        {
            this.logger?.LogWarning("[{LogKey}] active entity persistence failed (entityType={EntityType}, errorType={ErrorType})", "LiteDB", typeof(TEntity).Name, exception.GetType().Name);
            return Result<T>.Failure(new ExceptionError(exception));
        }
    }

    private static FindOptions<TEntity> FirstOptions(IFindOptions<TEntity> options) => new()
    {
        Skip = options?.Skip, Take = options?.Take <= 0 ? options.Take : 1, Distinct = options?.Distinct,
        Order = options?.Order, Orders = options?.Orders, Include = options?.Include,
        Includes = options?.Includes, Hierarchy = options?.Hierarchy, NoTracking = options?.NoTracking ?? false
    };
}
