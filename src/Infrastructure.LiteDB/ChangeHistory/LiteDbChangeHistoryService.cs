// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Reflection;
using System.Text.Json;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Provides authorized history queries and transactional restore through portable DevKit contracts.</summary>
/// <typeparam name="TEntity">The entity whose history is exposed.</typeparam>
/// <param name="session">The entity database session.</param>
/// <param name="options">The frozen capture and query policies.</param>
/// <param name="entityCollection">The registered entity collection to restore.</param>
/// <param name="services">The scoped authorizers and domain restore handlers.</param>
public sealed partial class LiteDbChangeHistoryService<TEntity>(LiteDbDatabaseSession session, LiteDbChangeHistoryOptions options,
    string entityCollection, IServiceProvider services) : IChangeHistoryService<TEntity, LiteDbContext> where TEntity : class, IEntity
{
    /// <inheritdoc />
    public Task<Result<ChangeHistoryFindAllResult>> FindAllAsync(ChangeHistoryFindAllQuery query, CancellationToken cancellationToken = default) =>
        this.GuardAsync(async () =>
        {
            query ??= new();
            await this.AuthorizeAsync("Read", query.EntityId, null, cancellationToken).ConfigureAwait(false);
            return await session.ExecuteAsync(async () => await session.NativeAsync(database =>
            {
                var page = Math.Max(query.Page, 1);
                var size = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, options.MaxMaterializedRows);
                var native = this.BuildQuery(database, query);
                var count = native.LongCount();
                var ordered = query.OrderAscending ? this.BuildQuery(database, query).OrderBy("$.ChangedDateTicks") : this.BuildQuery(database, query).OrderByDescending("$.ChangedDateTicks");
                ordered = ordered.ThenBy("$._id");
                var rows = ordered.Skip(checked((page - 1) * size)).Limit(size).ToList().Select(d => LiteDbChangeHistoryStorage.Read(d, query.IncludeValues)).ToArray();
                var pages = checked((int)Math.Ceiling((double)count / size));
                return Result<ChangeHistoryFindAllResult>.Success(new(rows, count, page, size, pages, page > 1, page < pages));
            }, cancellationToken).ConfigureAwait(false), false, cancellationToken).ConfigureAwait(false);
        });

    /// <inheritdoc />
    public Task<Result<ChangeHistoryFindAllChangeSetsResult>> FindAllChangeSetsAsync(ChangeHistoryFindAllQuery query, CancellationToken cancellationToken = default) =>
        this.GuardAsync(async () =>
        {
            query ??= new();
            await this.AuthorizeAsync("Read", query.EntityId, null, cancellationToken).ConfigureAwait(false);
            return await session.ExecuteAsync(async () => await session.NativeAsync(database =>
            {
                // Group before paging so no change set loses rows at a page boundary.
                var matching = this.Limited(this.BuildQuery(database, query)).Select(d => LiteDbChangeHistoryStorage.Read(d, query.IncludeValues)).ToArray();
                var ids = matching.Select(r => r.ChangeSetId).ToHashSet();
                var all = new List<ChangeHistoryChangeSetRecord>();
                var materialized = 0;
                foreach (var id in ids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rows = this.Limited(this.EntityQuery(database).Where(BsonExpression.Create("$.ChangeSetId = @0", id)))
                        .Select(d => LiteDbChangeHistoryStorage.Read(d, query.IncludeValues)).OrderBy(r => r.ChangeSetSequence).ToArray();
                    materialized = checked(materialized + rows.Length);
                    if (materialized > options.MaxMaterializedRows) { throw new LiteDbProviderException(new InvalidOperationError("The grouped history selection exceeds MaxMaterializedRows. Narrow the filters.")); }

                    all.Add(Group(rows));
                }

                var sorted = query.OrderAscending ? all.OrderBy(r => r.ChangedDate).ThenBy(r => r.ChangeSetId) : all.OrderByDescending(r => r.ChangedDate).ThenBy(r => r.ChangeSetId);
                var page = Math.Max(query.Page, 1);
                var size = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, options.MaxMaterializedRows);
                var pages = (int)Math.Ceiling((double)all.Count / size);
                return Result<ChangeHistoryFindAllChangeSetsResult>.Success(new(sorted.Skip(checked((page - 1) * size)).Take(size).ToArray(), all.Count, page, size, pages, page > 1, page < pages));
            }, cancellationToken).ConfigureAwait(false), false, cancellationToken).ConfigureAwait(false);
        });

    /// <inheritdoc />
    public Task<Result<ChangeHistoryChangeSetRecord>> FindOneChangeSetAsync(ChangeHistoryFindOneChangeSetQuery query, CancellationToken cancellationToken = default) =>
        this.GuardAsync(async () =>
        {
            if (query is null) { return Result<ChangeHistoryChangeSetRecord>.Failure(new InvalidInputError("A history query is required.")); }

            await this.AuthorizeAsync("Read", query.EntityId, null, cancellationToken).ConfigureAwait(false);
            return await session.ExecuteAsync(async () => await session.NativeAsync(database =>
            {
                var native = this.EntityQuery(database).Where(BsonExpression.Create("$.ChangeSetId = @0", query.ChangeSetId));
                if (query.EntityId is not null) { native = native.Where(BsonExpression.Create("$.EntityId = @0", query.EntityId)); }

                if (query.EntityType is not null) { native = native.Where(BsonExpression.Create("$.EntityType = @0", query.EntityType)); }

                var rows = this.Limited(native).Select(d => LiteDbChangeHistoryStorage.Read(d, query.IncludeValues)).OrderBy(r => r.ChangeSetSequence).ToArray();
                return rows.Length == 0 ? Result<ChangeHistoryChangeSetRecord>.Failure(new NotFoundError()) : Result<ChangeHistoryChangeSetRecord>.Success(Group(rows));
            }, cancellationToken).ConfigureAwait(false), false, cancellationToken).ConfigureAwait(false);
        });

    private ILiteQueryable<BsonDocument> EntityQuery(LiteDatabase database) => database.GetCollection<BsonDocument>(options.CollectionName).Query()
        .Where(BsonExpression.Create("$.EntityKey = @0", typeof(TEntity).AssemblyQualifiedNameShort()));

    private ILiteQueryable<BsonDocument> BuildQuery(LiteDatabase database, ChangeHistoryFindAllQuery query)
    {
        var native = this.EntityQuery(database);
        foreach (var (name, value) in new (string, object)[]
        {
            ("EntityType", query.EntityType), ("EntityId", query.EntityId), ("PropertyName", query.PropertyName),
            ("ChangeSetId", query.ChangeSetId), ("BulkOperationId", query.BulkOperationId), ("ChangedByUserId", query.ChangedByUserId),
            ("Operation", query.Operation), ("CaptureSource", query.CaptureSource), ("CaptureStrategy", query.CaptureStrategy), ("CaptureStatus", query.CaptureStatus)
        })
        {
            if (value is not null) { native = native.Where(BsonExpression.Create("$." + name + " = @0", new BsonValue(value))); }
        }

        if (query.ChangedDateFrom.HasValue) { native = native.Where(BsonExpression.Create("$.ChangedDateTicks >= @0", query.ChangedDateFrom.Value.UtcTicks)); }

        if (query.ChangedDateTo.HasValue) { native = native.Where(BsonExpression.Create("$.ChangedDateTicks <= @0", query.ChangedDateTo.Value.UtcTicks)); }

        return native;
    }

    private IReadOnlyList<BsonDocument> Limited(ILiteQueryable<BsonDocument> query)
    {
        var rows = query.Limit(options.MaxMaterializedRows + 1).ToList();
        if (rows.Count > options.MaxMaterializedRows) { throw new LiteDbProviderException(new InvalidOperationError("The history selection exceeds MaxMaterializedRows. Narrow the filters.")); }

        return rows;
    }

    private async Task AuthorizeAsync(string operation, string entityId, string reason, CancellationToken cancellationToken)
    {
        var policy = operation == "Read" ? options.ReadAuthorizationPolicy : options.RestoreAuthorizationPolicy;
        if (string.IsNullOrWhiteSpace(policy)) { return; }

        var authorizer = services.GetService<ILiteDbChangeHistoryAuthorizer>();
        if (authorizer is null) { throw new LiteDbProviderException(new UnauthorizedError("No history policy authorizer is registered.")); }

        var result = await authorizer.AuthorizeAsync(new(typeof(TEntity), entityId, policy, operation, reason), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { throw new LiteDbProviderException(result); }
    }

    private static ChangeHistoryChangeSetRecord Group(IReadOnlyList<ChangeHistoryRecord> rows)
    {
        var first = rows[0];
        return new(first.ChangeSetId, first.EntityType, first.EntityId, first.ChangedDate, first.ChangedByUserId, first.ChangedByUserName,
            first.ChangedByEmail, first.Operation, first.CaptureSource, first.BulkOperationId, rows);
    }

    private async Task<Result<T>> GuardAsync<T>(Func<Task<Result<T>>> action)
    {
        try { return await action().ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (LiteDbProviderException exception) { return Result<T>.Failure().WithErrors(exception.Errors).WithMessages(exception.Messages); }
        catch (Exception exception) when (!exception.IsTransientException()) { return Result<T>.Failure(new ExceptionError(exception)); }
    }
}
