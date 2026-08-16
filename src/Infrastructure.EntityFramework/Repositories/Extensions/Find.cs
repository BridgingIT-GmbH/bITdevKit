// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Reflection;

public static partial class DbContextExtensions
{
    /// <summary>
    /// Finds .
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="id">The entity identifier.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task<TEntity> FindAsync<TEntity>(
        this DbContext source,
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(source, nameof(source));

        if (id is null)
        {
            return default;
        }

        options ??= new FindOptions<TEntity>();
        var keyProperties = GetKeyProperties<TEntity>(source);
        if (keyProperties.Length != 1)
        {
            throw new Exception("Entity type has multiple Key properties defined.");
        }

        var parameter = Expression.Parameter(typeof(TEntity));
        Expression body = null;
        for (var i = 0; i < keyProperties.Length; i++)
        {
            Expression propertyEx = Expression.Property(parameter, keyProperties[i]);
            Expression valueEx = Expression.Constant(id);
            Expression conditionEx = Expression.Equal(propertyEx, valueEx);
            body = body is null ? conditionEx : Expression.AndAlso(body, conditionEx);
        }

        var filter = Expression.Lambda<Func<TEntity, bool>>(body, parameter);

        if (options?.NoTracking == true)
        {
            return await source.Set<TEntity>()
                .AsNoTracking()
                .IncludeIf(options)
                .FirstOrDefaultAsync(filter, cancellationToken);
        }

        return await source.Set<TEntity>().IncludeIf(options).FirstOrDefaultAsync(filter, cancellationToken);
    }

    /// <summary>
    /// Finds .
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TDatabaseEntity">The database entity type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="id">The entity identifier.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="mapper">The mapper used to transform values.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task<TDatabaseEntity> FindAsync<TEntity, TDatabaseEntity>(
        this DbContext source,
        object id,
        IFindOptions<TEntity> options = null,
        IEntityMapper mapper = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
        where TDatabaseEntity : class
    {
        EnsureArg.IsNotNull(source, nameof(source));

        if (id is null)
        {
            return default;
        }

        options ??= new FindOptions<TEntity>();
        var keyProperties = GetKeyProperties<TDatabaseEntity>(source);
        if (keyProperties.Length != 1)
        {
            throw new Exception("Database Entity type has multiple Key properties defined.");
        }

        var parameter = Expression.Parameter(typeof(TDatabaseEntity));
        Expression body = null;
        for (var i = 0; i < keyProperties.Length; i++)
        {
            Expression propertyEx = Expression.Property(parameter, keyProperties[i]);
            Expression valueEx = Expression.Constant(id);
            Expression conditionEx = Expression.Equal(propertyEx, valueEx);
            body = body is null ? conditionEx : Expression.AndAlso(body, conditionEx);
        }

        var filter = Expression.Lambda<Func<TDatabaseEntity, bool>>(body, parameter);

        if (options?.NoTracking == true)
        {
            return await source.Set<TDatabaseEntity>()
                .AsNoTracking()
                .IncludeIf(options, mapper)
                .FirstOrDefaultAsync(filter, cancellationToken);
        }

        return await source.Set<TDatabaseEntity>()
            .IncludeIf(options, mapper)
            .FirstOrDefaultAsync(filter, cancellationToken);
    }

    /// <summary>
    /// Gets key properties.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <returns>The result of the operation.</returns>
    public static PropertyInfo[] GetKeyProperties<T>(this DbContext source)
    {
        return [.. source.Model?.FindEntityType(typeof(T))?
            .FindPrimaryKey()?.Properties?.Select(p => p.PropertyInfo).SafeNull()];
    }
}
