// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

//namespace BridgingIT.DevKit.Infrastructure.LiteDB.Repositories
//{
//    using System;
//    using System.Linq;
//    using System.Linq.Expressions;
//    using BridgingIT.DevKit.Domain.Model;
//    using BridgingIT.DevKit.Domain.Repositories;
//    using EnsureThat;

//    public static partial class Extensions
//    {
//        public static ILiteQueryableResult<TEntity> DistinctIf<TEntity>(
//            this ILiteQueryableResult<TEntity> source,
//            IFindOptions<TEntity> options)
//            where TEntity : class, IEntity
//        {
//            if (options?.Distinct is null && options?.Distinct?.Expression is null)
//            {
//                return source;
//            }

//            source = options.Distinct is not null && options.Distinct.Expression is null
//                ? source.Distinct()
//                : source
//                    .AsEnumerable() // client evaluation, needed by groupby https://docs.microsoft.com/en-us/ef/core/querying/client-eval
//                                    // (net6.0) better db support for groupby/distinct is planned for ef core 6.0, this would remove the need for the current client side evaluation (AsEnumerable)
//                    .GroupBy(options.Distinct.Expression.Compile())
//                    .Select(g => g.FirstOrDefault())
//                    .AsQueryable();

//            return source;
//        }

//        public static ILiteQueryableResult<TDatabaseEntity> DistinctByIf<TEntity, TDatabaseEntity>(
//            this ILiteQueryable<TDatabaseEntity> source,
//            IFindOptions<TEntity> options,
//            IEntityMapper mapper)
//            where TEntity : class, IEntity
//            where TDatabaseEntity : class
//        {
//            EnsureArg.IsNotNull(mapper, nameof(mapper));

//            if (options?.Distinct is null && options?.Distinct?.Expression is null)
//            {
//                return source;
//            }

//            source = options.Distinct is not null && options.Distinct.Expression is null
//                ? source.Distinct()
//                : source
//                    .AsEnumerable() // client evaluation, needed by groupby https://docs.microsoft.com/en-us/ef/core/querying/client-eval
//                                    // (net6.0) better db support for groupby/distinct is planned for ef core 6.0, this would remove the need for the current client side evaluation (AsEnumerable)
//                    .GroupBy(mapper.MapExpression<Expression<Func<TDatabaseEntity, object>>>(options.Distinct.Expression)?.Compile())
//                    .Select(g => g.FirstOrDefault())
//                    .AsQueryable();

//            return source;
//        }

//        public static IQueryable<TProjection> DistinctIf<TProjection, TEntity>(
//            this IQueryable<TProjection> source,
//            IFindOptions<TEntity> options,
//            IEntityMapper mapper)
//            where TEntity : class, IEntity
//        {
//            if (options?.Distinct is null/* && options?.Distinct?.Expression is null*/)
//            {
//                return source;
//            }

//            source = options.Distinct is not null && options.Distinct.Expression is null
//                ? source.Distinct()
//                : source
//                    .AsEnumerable() // client evaluation, needed by groupby https://docs.microsoft.com/en-us/ef/core/querying/client-eval
//                                    // (net6.0) better db support for groupby/distinct is planned for ef core 6.0, this would remove the need for the current client side evaluation (AsEnumerable)
//                    .GroupBy(mapper.MapExpression<Expression<Func<TProjection, object>>>(options.Distinct.Expression)?.Compile())
//                    //.GroupBy(options.Distinct.Expression.Compile())
//                    .Select(g => g.FirstOrDefault())
//                    .AsQueryable();

//            return source;
//        }
//    }
//}

