// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Diagnostics.Metrics;
using System.Linq.Expressions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Emits ActiveEntity read, write, and delete metrics for ActiveEntity operations.
/// </summary>
/// <typeparam name="TEntity">The ActiveEntity type.</typeparam>
/// <example>
/// <code>
/// cfg.For&lt;Customer, CustomerId&gt;()
///     .UseEntityFrameworkProvider(o => o.Context&lt;AppDbContext&gt;())
///     .AddMetricsBehavior();
/// </code>
/// </example>
public class ActiveEntityMetricsBehavior<TEntity>(IMeterFactory meterFactory = null) : ActiveEntityBehaviorBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly AsyncLocal<Stack<OperationState>> operations = new();
    private readonly string entityName = Metrics.NormalizeTypeName(typeof(TEntity));

    public override Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_write", "insert", cancellationToken);

    public override Task<Result> AfterInsertAsync(TEntity entity, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_write", "insert", success, cancellationToken);

    public override Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_write", "update", cancellationToken);

    public override Task<Result> AfterUpdateAsync(TEntity entity, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_write", "update", success, cancellationToken);

    public override Task<Result> BeforeUpdateSetAsync(CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_write", "update_set", cancellationToken);

    public override Task<Result> AfterUpdateSetAsync(bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_write", "update_set", success, cancellationToken);

    public override Task<Result> BeforeUpsertAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_write", "upsert", cancellationToken);

    public override Task<Result> AfterUpsertAsync(TEntity entity, RepositoryActionResult action, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_write", "upsert", success, cancellationToken);

    public override Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_delete", "delete", cancellationToken);

    public override Task<Result> AfterDeleteAsync(TEntity entity, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_delete", "delete", success, cancellationToken);

    public override Task<Result> BeforeDeleteAsync(object id, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_delete", "delete", cancellationToken);

    public override Task<Result> AfterDeleteAsync(object id, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_delete", "delete", success, cancellationToken);

    public override Task<Result> BeforeDeleteSetAsync(CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_delete", "delete_set", cancellationToken);

    public override Task<Result> AfterDeleteSetAsync(bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_delete", "delete_set", success, cancellationToken);

    public override Task<Result> BeforeFindOneAsync(object id, IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "find_one", cancellationToken);

    public override Task<Result> AfterFindOneAsync(object id, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "find_one", success, cancellationToken);

    public override Task<Result> BeforeFindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "find_one", cancellationToken);

    public override Task<Result> AfterFindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "find_one", success, cancellationToken);

    public override Task<Result> BeforeFindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "find_one", cancellationToken);

    public override Task<Result> AfterFindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "find_one", success, cancellationToken);

    public override Task<Result> BeforeFindOneAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "find_one", cancellationToken);

    public override Task<Result> AfterFindOneAsync(FilterModel filter, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "find_one", success, cancellationToken);

    public override Task<Result> BeforeFindAllAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "find_all", cancellationToken);

    public override Task<Result> AfterFindAllAsync(IFindOptions<TEntity> options, IEnumerable<TEntity> entities, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "find_all", success, cancellationToken);

    public override Task<Result> BeforeFindAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "find_all", cancellationToken);

    public override Task<Result> AfterFindAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, IEnumerable<TEntity> entities, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "find_all", success, cancellationToken);

    public override Task<Result> BeforeFindAllPagedAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "find_all_paged", cancellationToken);

    public override Task<Result> AfterFindAllPagedAsync(IFindOptions<TEntity> options, ResultPaged<TEntity> result, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "find_all_paged", success, cancellationToken);

    public override Task<Result> BeforeProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "project_all", cancellationToken);

    public override Task<Result> AfterProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, IEnumerable<TProjection> entities, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "project_all", success, cancellationToken);

    public override Task<Result> BeforeProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "project_all", cancellationToken);

    public override Task<Result> AfterProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, IEnumerable<TProjection> entities, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "project_all", success, cancellationToken);

    public override Task<Result> BeforeProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "project_all", cancellationToken);

    public override Task<Result> AfterProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, IEnumerable<TProjection> entities, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "project_all", success, cancellationToken);

    public override Task<Result> BeforeProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "project_all_paged", cancellationToken);

    public override Task<Result> AfterProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, ResultPaged<TProjection> result, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "project_all_paged", success, cancellationToken);

    public override Task<Result> BeforeExistsAsync(CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "exists", cancellationToken);

    public override Task<Result> AfterExistsAsync(bool exists, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "exists", success, cancellationToken);

    public override Task<Result> BeforeExistsAsync(object id, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "exists", cancellationToken);

    public override Task<Result> AfterExistsAsync(object id, bool exists, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "exists", success, cancellationToken);

    public override Task<Result> BeforeExistsAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "exists", cancellationToken);

    public override Task<Result> AfterExistsAsync(ISpecification<TEntity> specification, bool exists, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "exists", success, cancellationToken);

    public override Task<Result> BeforeExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "exists", cancellationToken);

    public override Task<Result> AfterExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, bool exists, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "exists", success, cancellationToken);

    public override Task<Result> BeforeExistsAsync(FilterModel filter, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "exists", cancellationToken);

    public override Task<Result> AfterExistsAsync(FilterModel filter, bool exists, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "exists", success, cancellationToken);

    public override Task<Result> BeforeCountAsync(CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "count", cancellationToken);

    public override Task<Result> AfterCountAsync(long count, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "count", success, cancellationToken);

    public override Task<Result> BeforeCountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        this.BeginAsync("activeentity_read", "count", cancellationToken);

    public override Task<Result> AfterCountAsync(ISpecification<TEntity> specification, long count, bool success, CancellationToken cancellationToken = default) =>
        this.EndAsync("activeentity_read", "count", success, cancellationToken);

    private Task<Result> BeginAsync(string family, string operation, CancellationToken cancellationToken)
    {
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Success());
        }

        var series = Metrics.Series(family);
        var typedSeries = Metrics.Series(family, this.entityName, operation);
        Metrics.Increment(meterFactory, series);
        Metrics.Increment(meterFactory, typedSeries);
        this.Operations.Push(new OperationState(family, operation, series, typedSeries, Metrics.StartTimestamp()));

        return Task.FromResult(Result.Success());
    }

    private Task<Result> EndAsync(string family, string operation, bool success, CancellationToken cancellationToken)
    {
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Success());
        }

        var state = this.PopOperation(family, operation);
        if (state is null)
        {
            return Task.FromResult(Result.Success());
        }

        if (!success)
        {
            Metrics.Increment(meterFactory, Metrics.FailureSeries(state.Series));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(state.TypedSeries));
        }

        Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(state.Series), state.StartedTimestamp);
        Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(state.TypedSeries), state.StartedTimestamp);

        return Task.FromResult(Result.Success());
    }

    private Stack<OperationState> Operations
    {
        get
        {
            this.operations.Value ??= new Stack<OperationState>();
            return this.operations.Value;
        }
    }

    private OperationState PopOperation(string family, string operation)
    {
        if (this.Operations.Count == 0)
        {
            return null;
        }

        var state = this.Operations.Pop();
        return string.Equals(state.Family, family, StringComparison.Ordinal) &&
            string.Equals(state.Operation, operation, StringComparison.Ordinal)
                ? state
                : null;
    }

    private sealed record OperationState(string Family, string Operation, string Series, string TypedSeries, long StartedTimestamp);
}
