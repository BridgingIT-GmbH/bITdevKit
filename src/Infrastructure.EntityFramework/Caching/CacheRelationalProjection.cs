// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Bounds persisted cache entity fields on the database before CLR materialization.</summary>
/// <remarks>Selectors use trusted entity annotations and fixed JSON-format ceilings. Oversized binary fields are not retrieved, and clipped scalar fields retain one excess character so corruption cannot become a truncated identity.</remarks>
/// <example><code>var row = await CacheRelationalProjection.SingleAsync(context, context.Set&lt;CacheStoreEntity&gt;().Where(entity =&gt; entity.Id == "root"), true, token);</code></example>
public static class CacheRelationalProjection
{
    /// <summary>Projects bounded scalar fields for a cache entity query whose binary fields have separate acquisition guards.</summary>
    /// <typeparam name="TEntity">The trusted annotated cache entity type.</typeparam>
    /// <param name="query">The native query retaining its existing predicates and ordering.</param>
    /// <returns>A server-side bounded projection without implicit change tracking.</returns>
    public static IQueryable<TEntity> Select<TEntity>(IQueryable<TEntity> query) where TEntity : class, new() =>
        query.AsNoTracking().Select(Plan<TEntity>.Entity);

    /// <summary>Reads exactly one bounded entity and optionally reuses or attaches its tracked identity.</summary>
    /// <typeparam name="TEntity">The trusted annotated cache entity type.</typeparam>
    /// <param name="context">The independently owned native context.</param>
    /// <param name="query">The query selecting exactly one native row.</param>
    /// <param name="tracking">Whether mutation code needs a tracked entity.</param>
    /// <param name="cancellationToken">Cancels the native bounded read.</param>
    /// <returns>The validated entity.</returns>
    public static async Task<TEntity> SingleAsync<TEntity>(DbContext context, IQueryable<TEntity> query, bool tracking,
        CancellationToken cancellationToken) where TEntity : class, new()
    {
        var row = await query.AsNoTracking().Select(Plan<TEntity>.Row).SingleAsync(cancellationToken).ConfigureAwait(false);
        return Materialize(context, row, tracking);
    }

    /// <summary>Reads at most one bounded entity and optionally reuses or attaches its tracked identity.</summary>
    /// <typeparam name="TEntity">The trusted annotated cache entity type.</typeparam>
    /// <param name="context">The independently owned native context.</param>
    /// <param name="query">The native query selecting at most one row.</param>
    /// <param name="tracking">Whether mutation code needs a tracked entity.</param>
    /// <param name="cancellationToken">Cancels the native bounded read.</param>
    /// <returns>The validated entity, or null when the row does not exist.</returns>
    public static async Task<TEntity> SingleOrDefaultAsync<TEntity>(DbContext context, IQueryable<TEntity> query, bool tracking,
        CancellationToken cancellationToken) where TEntity : class, new()
    {
        var row = await query.AsNoTracking().Select(Plan<TEntity>.Row).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : Materialize(context, row, tracking);
    }

    /// <summary>Rejects any clipped or missing required persisted scalar before using the projected metadata.</summary>
    /// <typeparam name="TEntity">The trusted annotated cache entity type.</typeparam>
    /// <param name="entity">The bounded projected entity.</param>
    /// <returns>The same validated entity.</returns>
    /// <exception cref="CacheRelationalException">A persisted field violates its declared finite format.</exception>
    public static TEntity Validate<TEntity>(TEntity entity) where TEntity : class, new()
    {
        foreach (var field in Plan<TEntity>.Fields)
        {
            var value = field.Property.GetValue(entity);
            if ((value is null && field.Required) || value is string text && (text.Length > field.Maximum || field.Required && text.Length == 0) ||
                value is byte[] bytes && bytes.Length > field.Maximum)
            {
                throw new CacheRelationalException(CacheErrorCode.Corruption, "A persisted cache field exceeds its finite format.");
            }
        }

        return entity;
    }

    /// <summary>Checks a bounded scalar projection before it becomes an identity, cursor or cleanup accounting input.</summary>
    /// <param name="value">The server-projected value retaining at most one excess character.</param>
    /// <param name="maximum">The fixed persisted field maximum.</param>
    /// <param name="optional">Whether the format allows null.</param>
    /// <returns>The same validated scalar.</returns>
    /// <exception cref="CacheRelationalException">The scalar is missing or exceeds its finite format.</exception>
    public static string ValidateScalar(string value, int maximum, bool optional = false)
    {
        if (value is null ? !optional : value.Length > maximum || !optional && value.Length == 0)
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "A persisted cache scalar exceeds its finite format.");
        }

        return value;
    }

    private static TEntity Materialize<TEntity>(DbContext context, CacheRelationalProjectionRow<TEntity> row, bool tracking) where TEntity : class, new()
    {
        if (row.OversizedBinary) { throw new CacheRelationalException(CacheErrorCode.Corruption, "A persisted cache binary field exceeds its finite format."); }

        var entity = Validate(row.Value);
        if (!tracking) { return entity; }

        var key = context.Model.FindEntityType(typeof(TEntity)).FindPrimaryKey();
        foreach (var entry in context.ChangeTracker.Entries<TEntity>())
        {
            if (key.Properties.All(property => Equals(property.PropertyInfo.GetValue(entry.Entity), property.PropertyInfo.GetValue(entity))))
            {
                return entry.Entity;
            }
        }

        context.Attach(entity);
        return entity;
    }

    private sealed record Field(PropertyInfo Property, int Maximum, bool Required);

    private static class Plan<TEntity> where TEntity : class, new()
    {
        public static readonly Field[] Fields = typeof(TEntity).GetProperties().Where(property => property.PropertyType == typeof(string) || property.PropertyType == typeof(byte[]))
            .Select(property => new Field(property, Maximum(property), property.IsDefined(typeof(RequiredAttribute)))).ToArray();

        public static readonly Expression<Func<TEntity, TEntity>> Entity = BuildEntity();

        public static readonly Expression<Func<TEntity, CacheRelationalProjectionRow<TEntity>>> Row = BuildRow();

        private static int Maximum(PropertyInfo property) => property.GetCustomAttribute<MaxLengthAttribute>()?.Length ?? property.Name switch
        {
            nameof(CacheEntryEntity.MetadataJson) => 65536,
            nameof(CacheContractEntity.SchemaJson) => 16384,
            nameof(CacheContractEntity.PolicyJson) => 65536,
            _ => throw new InvalidOperationException("A cache entity field has no finite persisted format.")
        };

        private static Expression<Func<TEntity, TEntity>> BuildEntity()
        {
            var parameter = Expression.Parameter(typeof(TEntity), "entity");
            var bindings = new List<MemberBinding>();
            foreach (var property in typeof(TEntity).GetProperties().Where(property => property.CanWrite &&
                (property.PropertyType.IsValueType || property.PropertyType == typeof(string) || property.PropertyType == typeof(byte[]))))
            {
                var value = (Expression)Expression.Property(parameter, property);
                if (property.PropertyType == typeof(string))
                {
                    value = Expression.Condition(Expression.Equal(value, Expression.Constant(null, typeof(string))), Expression.Constant(null, typeof(string)),
                        Expression.Call(value, nameof(string.Substring), Type.EmptyTypes, Expression.Constant(0), Expression.Constant(Maximum(property) + 1)));
                }
                else if (property.PropertyType == typeof(byte[]))
                {
                    value = Expression.Condition(BinaryOverflow(value, Maximum(property)), Expression.Constant(null, typeof(byte[])), value);
                }

                bindings.Add(Expression.Bind(property, value));
            }

            return Expression.Lambda<Func<TEntity, TEntity>>(Expression.MemberInit(Expression.New(typeof(TEntity)), bindings), parameter);
        }

        private static Expression<Func<TEntity, CacheRelationalProjectionRow<TEntity>>> BuildRow()
        {
            var parameter = Entity.Parameters[0];
            var overflow = (Expression)Expression.Constant(false);
            foreach (var field in Fields.Where(field => field.Property.PropertyType == typeof(byte[])))
            {
                overflow = Expression.OrElse(overflow, BinaryOverflow(Expression.Property(parameter, field.Property), field.Maximum));
            }

            return Expression.Lambda<Func<TEntity, CacheRelationalProjectionRow<TEntity>>>(Expression.New(
                typeof(CacheRelationalProjectionRow<TEntity>).GetConstructors().Single(), Entity.Body, overflow), parameter);
        }

        private static Expression BinaryOverflow(Expression value, int maximum) => Expression.AndAlso(
            Expression.NotEqual(value, Expression.Constant(null, typeof(byte[]))), Expression.GreaterThan(Expression.ArrayLength(value), Expression.Constant(maximum)));
    }
}

/// <summary>Transfers one bounded entity and an explicit server-computed oversized-binary marker to its native reader.</summary>
/// <typeparam name="TEntity">The trusted cache entity type.</typeparam>
/// <param name="Value">The projected bounded entity.</param>
/// <param name="OversizedBinary">Whether a binary field was omitted because it exceeded its native format.</param>
/// <example><code>if (row.OversizedBinary) { throw new CacheRelationalException(CacheErrorCode.Corruption, "Invalid native field."); }</code></example>
public sealed record CacheRelationalProjectionRow<TEntity>(TEntity Value, bool OversizedBinary) where TEntity : class;
