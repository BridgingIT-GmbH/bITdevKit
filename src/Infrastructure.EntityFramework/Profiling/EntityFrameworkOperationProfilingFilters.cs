// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Linq.Expressions;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

internal static class EntityFrameworkOperationProfilingFilters
{
    internal static IQueryable<OperationProfilingEntity> Apply<TContext>(TContext context, OperationProfilingQuery query, long watermark)
        where TContext : DbContext, IProfilingDbContext
    {
        var lower = query.FromUtc.Value.UtcTicks;
        var upper = query.ToUtc.Value.UtcTicks;
        var roots = context.Set<OperationProfilingEntity>().AsNoTracking().Where(root => root.CommitWatermark <= watermark);
        roots = query.IntervalOverlap ? roots.Where(root => root.StartedUtcTicks < upper && (root.CompletedUtcTicks > lower || root.StartedUtcTicks == root.CompletedUtcTicks && root.StartedUtcTicks >= lower))
            : roots.Where(root => root.CompletedUtcTicks >= lower && root.CompletedUtcTicks < upper);
        if (query.Id.HasValue) { roots = roots.Where(root => root.Id == query.Id.Value); }

        if (query.ParentOperationId.HasValue) { roots = roots.Where(root => root.ParentOperationId == query.ParentOperationId.Value); }

        if (query.NodeId.HasValue) { roots = roots.Where(root => root.NodeId == query.NodeId.Value); }

        if (query.HasSegmentFailures.HasValue) { roots = roots.Where(root => root.HasSegmentFailures == query.HasSegmentFailures.Value); }

        if (query.HttpStatusCode.HasValue) { roots = roots.Where(root => root.HttpStatusCode == query.HttpStatusCode.Value); }

        if (query.Outcomes.Count > 0)
        {
            var outcomes = query.Outcomes.ToArray();
            roots = roots.Where(root => outcomes.Contains(root.Outcome));
        }

        if (query.Key is not null)
        {
            var bytes = ProfilingOperationComparisons.Exact(query.Key);
            var hash = ProfilingOperationComparisons.Hash(bytes);
            roots = roots.Where(root => root.KeyHash == hash && root.KeyBytes == bytes);
        }

        if (query.Kind is not null)
        {
            var bytes = ProfilingOperationComparisons.Exact(query.Kind);
            var hash = ProfilingOperationComparisons.Hash(bytes);
            roots = roots.Where(root => root.KindHash == hash && root.KindBytes == bytes);
        }

        if (query.Route is not null)
        {
            var bytes = ProfilingOperationComparisons.Exact(query.Route);
            var hash = ProfilingOperationComparisons.Hash(bytes);
            roots = roots.Where(root => root.HttpRouteHash == hash && root.HttpRouteBytes == bytes);
        }

        roots = Binary(roots, query.CorrelationId, root => root.CorrelationBytes);
        roots = Binary(roots, query.ApplicationVersion, root => root.ApplicationVersionBytes);
        roots = Binary(roots, query.ApplicationRequestId, root => root.ApplicationRequestIdBytes);
        roots = Binary(roots, query.HttpMethod, root => root.HttpMethodBytes);
        roots = Binary(roots, query.SamplingStrategyKey, root => root.SamplingStrategyBytes);
        roots = Binary(roots, query.SamplingConfigurationKey, root => root.SamplingConfigurationBytes);
        foreach (var predicate in query.Dimensions.Concat(query.GroupDimensions))
        {
            var match = Dimension(predicate, root: true);
            var parameter = Expression.Parameter(typeof(OperationProfilingEntity), "root");
            var navigation = Expression.Call(typeof(Queryable), nameof(Queryable.AsQueryable), [typeof(OperationProfilingDimensionEntity)], Expression.Property(parameter, nameof(OperationProfilingEntity.Dimensions)));
            var any = Expression.Call(typeof(Queryable), nameof(Queryable.Any), [typeof(OperationProfilingDimensionEntity)], navigation, match);
            roots = roots.Where(Expression.Lambda<Func<OperationProfilingEntity, bool>>(predicate.Operator == ProfilingDimensionOperator.Missing ? Expression.Not(any) : any, parameter));
        }

        if (query.SegmentKey is not null || query.SegmentPath is not null || query.SegmentDimensions.Count > 0 || query.SegmentOutcomes.Count > 0)
        {
            Expression<Func<OperationProfilingSegmentEntity, bool>> segment = _ => true;
            if (query.SegmentKey is not null)
            {
                var bytes = ProfilingOperationComparisons.Exact(query.SegmentKey);
                var hash = ProfilingOperationComparisons.Hash(bytes);
                segment = And(segment, summary => summary.KeyHash == hash && summary.KeyBytes == bytes);
            }

            if (query.SegmentPath is not null)
            {
                var bytes = ProfilingOperationComparisons.Exact(query.SegmentPath.ComparisonKey);
                var hash = ProfilingOperationComparisons.Hash(bytes);
                segment = And(segment, summary => summary.PathHash == hash && summary.PathBytes == bytes);
            }

            if (query.SegmentOutcomes.Count > 0)
            {
                var completed = query.SegmentOutcomes.Contains(ProfilingSegmentOutcome.Completed);
                var failed = query.SegmentOutcomes.Contains(ProfilingSegmentOutcome.Failed);
                var canceled = query.SegmentOutcomes.Contains(ProfilingSegmentOutcome.Canceled);
                var incomplete = query.SegmentOutcomes.Contains(ProfilingSegmentOutcome.Incomplete);
                segment = And(segment, summary => completed && summary.CompletedCount > 0 || failed && summary.FailedCount > 0
                    || canceled && summary.CanceledCount > 0 || incomplete && summary.IncompleteCount > 0);
            }

            foreach (var predicate in query.SegmentDimensions)
            {
                var match = Dimension(predicate, root: false);
                var summary = Expression.Parameter(typeof(OperationProfilingSegmentEntity), "summary");
                var dimension = match.Parameters[0];
                var owner = Expression.Equal(Expression.Property(dimension, nameof(OperationProfilingDimensionEntity.SegmentId)), Expression.Convert(Expression.Property(summary, nameof(OperationProfilingSegmentEntity.Id)), typeof(Guid?)));
                var scoped = Expression.Lambda<Func<OperationProfilingDimensionEntity, bool>>(Expression.AndAlso(owner, match.Body), dimension);
                var any = Expression.Call(typeof(Queryable), nameof(Queryable.Any), [typeof(OperationProfilingDimensionEntity)], ((IQueryable<OperationProfilingDimensionEntity>)context.Set<OperationProfilingDimensionEntity>()).Expression, scoped);
                segment = And(segment, Expression.Lambda<Func<OperationProfilingSegmentEntity, bool>>(predicate.Operator == ProfilingDimensionOperator.Missing ? Expression.Not(any) : any, summary));
            }

            roots = roots.Where(root => root.Segments.AsQueryable().Any(segment));
        }

        return roots;
    }

    private static IQueryable<OperationProfilingEntity> Binary(IQueryable<OperationProfilingEntity> query, string text, Expression<Func<OperationProfilingEntity, byte[]>> field)
    {
        if (text is null)
        {
            return query;
        }

        var bytes = ProfilingOperationComparisons.Exact(text);
        return query.Where(Expression.Lambda<Func<OperationProfilingEntity, bool>>(Expression.Equal(field.Body, Expression.Constant(bytes)), field.Parameters));
    }

    private static Expression<Func<OperationProfilingDimensionEntity, bool>> Dimension(ProfilingDimensionPredicate predicate, bool root)
    {
        var name = ProfilingOperationComparisons.Exact(predicate.Key);
        var hash = ProfilingOperationComparisons.Hash(name);
        Expression<Func<OperationProfilingDimensionEntity, bool>> identity = dimension => dimension.KeyHash == hash && dimension.KeyBytes == name;
        if (root) { identity = And(identity, dimension => dimension.SegmentId == null); }

        if (predicate.Operator == ProfilingDimensionOperator.Equal)
        {
            var type = predicate.Value.Type;
            var value = ProfilingOperationComparisons.Exact(predicate.Value.Scalar);
            return And(identity, dimension => !dimension.Mixed && !dimension.Partial && dimension.ValueType == type && dimension.ValueBytes == value);
        }

        return And(identity, predicate.Operator == ProfilingDimensionOperator.Mixed
            ? dimension => dimension.Mixed : dimension => dimension.ValueType != null || dimension.Mixed);
    }

    private static Expression<Func<T, bool>> And<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right) =>
        Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left.Body, new ReplaceParameter(right.Parameters[0], left.Parameters[0]).Visit(right.Body)), left.Parameters);

    private sealed class ReplaceParameter(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : base.VisitParameter(node);
    }
}
