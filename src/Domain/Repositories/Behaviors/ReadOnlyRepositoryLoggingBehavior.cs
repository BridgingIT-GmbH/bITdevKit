// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Represents read only generic repository logging decorator.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
[Obsolete("Use ReadOnlyGenericRepositoryLoggingBehavior instead")]
public class ReadOnlyGenericRepositoryLoggingDecorator<TEntity> : ReadOnlyRepositoryLoggingBehavior<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Initializes a new instance of the <c>ReadOnlyGenericRepositoryLoggingDecorator</c> class.
    /// </summary>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="inner">The inner used by the operation.</param>
    public ReadOnlyGenericRepositoryLoggingDecorator(
        ILogger<IGenericRepository<TEntity>> logger,
        IGenericRepository<TEntity> inner)
        : base(logger, inner) { }

    /// <summary>
    /// Initializes a new instance of the <c>ReadOnlyGenericRepositoryLoggingDecorator</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="inner">The inner used by the operation.</param>
    public ReadOnlyGenericRepositoryLoggingDecorator(ILoggerFactory loggerFactory, IGenericRepository<TEntity> inner)
        : base(loggerFactory, inner) { }
}

/// <summary>
///     <para>Decorates an <see cref="IGenericRepository{TEntity}" />.</para>
///     <para>
///         .-----------.
///         | Decorator |
///         .-----------.        .------------.
///         `------------> | decoratee  |
///         (forward)    .------------.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <seealso cref="IGenericRepository{TEntity}" />
public partial class ReadOnlyRepositoryLoggingBehavior<TEntity> : IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
{
    private readonly string type;

    /// <summary>
    /// Initializes a new instance of the <c>ReadOnlyRepositoryLoggingBehavior</c> class.
    /// </summary>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="inner">The inner used by the operation.</param>
    public ReadOnlyRepositoryLoggingBehavior(
        ILogger<IGenericRepository<TEntity>> logger,
        IGenericRepository<TEntity> inner)
    {
        this.Logger = logger;
        this.Inner = inner;
        this.type = typeof(TEntity).Name;
    }

    /// <summary>
    /// Initializes a new instance of the <c>ReadOnlyRepositoryLoggingBehavior</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="inner">The inner used by the operation.</param>
    public ReadOnlyRepositoryLoggingBehavior(ILoggerFactory loggerFactory, IGenericRepository<TEntity> inner)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<IGenericRepository<TEntity>>() ??
            NullLoggerFactory.Instance.CreateLogger<IGenericRepository<TEntity>>();
        this.Inner = inner;
        this.type = typeof(TEntity).Name;
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    /// <summary>
    /// Gets the inner.
    /// </summary>
    protected IGenericRepository<TEntity> Inner { get; }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([specification], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogCount(this.Logger, Constants.LogKey, this.type);

        foreach (var specification in specifications.SafeNull())
        {
            this.Logger.LogDebug("[{LogKey}] repository specification: {Specification}",
                Constants.LogKey,
                specification.GetType().PrettyName());
        }

        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExists(this.Logger, Constants.LogKey, this.type, id);

        return await this.Inner.ExistsAsync(id, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return await this.Inner.FindAllAsync(options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);
        this.Logger.LogDebug("[{LogKey}] repository specification: {Specification}", Constants.LogKey, specification);

        return await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        foreach (var specification in specifications.SafeNull())
        {
            this.Logger.LogDebug("[{LogKey}] repository specification: {Specification}", Constants.LogKey, specification);
        }

        return await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        if (projection is not null)
        {
            this.Logger.LogDebug("[{LogKey}] repository: projection {Projection}", Constants.LogKey, projection);
        }

        return await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);
        this.Logger.LogDebug("[{LogKey}] repository specification: {Specification}", Constants.LogKey, specification);

        if (projection is not null)
        {
            this.Logger.LogDebug("[{LogKey}] repository: projection {Projection}", Constants.LogKey, projection);
        }

        return await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        if (projection is not null)
        {
            this.Logger.LogDebug("[{LogKey}] repository: projection {projection}", Constants.LogKey, projection);
        }

        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="id">The entity identifier.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOneId(this.Logger, Constants.LogKey, this.type, id);
        this.LogOptions(options);

        return await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOne(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);
        this.Logger.LogDebug("[{LogKey}] repository specification: {Specification}", Constants.LogKey, specification);

        return await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOne(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        foreach (var specification in specifications.SafeNull())
        {
            this.Logger.LogDebug("[{LogKey}] repository specification: {Specification}", Constants.LogKey, specification);
        }

        return await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();
    }

    private void LogOptions(IFindOptions<TEntity> options)
    {
        if (options?.Distinct?.Expression is not null)
        {
            this.Logger.LogDebug("[{LogKey}] repository: distinct {distinctExpression}",
                Constants.LogKey,
                options.Distinct.Expression);
        }

        foreach (var order in
                 (options?.Orders.EmptyToNull() ?? new List<OrderOption<TEntity>>()).Insert(options?.Order))
        {
            this.Logger.LogDebug("[{LogKey}] repository: order {orderExpression}", Constants.LogKey, order.Expression);
        }

        foreach (var include in (options?.Includes.EmptyToNull() ?? new List<IncludeOption<TEntity>>()).Insert(
                     options?.Include))
        {
            if (include.Expression is not null)
            {
                this.Logger.LogDebug("[{LogKey}] repository: include {includeExpression}",
                    Constants.LogKey,
                    include.Expression);
            }

            if (include.Path is not null)
            {
                this.Logger.LogDebug("[{LogKey}] repository: include {includePath}", Constants.LogKey, include.Path);
            }
        }

        if (options?.Skip.HasValue == true)
        {
            this.Logger.LogDebug("[{LogKey}] repository: skip {skip}", Constants.LogKey, options.Skip.Value);
        }

        if (options?.Take.HasValue == true)
        {
            this.Logger.LogDebug("[{LogKey}] repository: take {take}", Constants.LogKey, options.Take.Value);
        }
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the count operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="entityType">The name of the entity type.</param>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] repository: count (type={EntityType})")]
        public static partial void LogCount(ILogger logger, string logKey, string entityType);

        /// <summary>
        /// Writes a log entry for the exists operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="entityType">The name of the entity type.</param>
        /// <param name="entityId">The entity identifier.</param>
        [LoggerMessage(2, LogLevel.Information, "[{LogKey}] repository: exists (type={EntityType}, id={EntityId})")]
        public static partial void LogExists(ILogger logger, string logKey, string entityType, object entityId);

        /// <summary>
        /// Writes a log entry for the find all operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="entityType">The name of the entity type.</param>
        [LoggerMessage(3, LogLevel.Information, "[{LogKey}] repository: findall (type={EntityType})")]
        public static partial void LogFindAll(ILogger logger, string logKey, string entityType);

        /// <summary>
        /// Writes a log entry for the project all operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="entityType">The name of the entity type.</param>
        [LoggerMessage(4, LogLevel.Information, "[{LogKey}] repository: projectall (type={EntityType})")]
        public static partial void LogProjectAll(ILogger logger, string logKey, string entityType);

        /// <summary>
        /// Writes a log entry for the find one id operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="entityType">The name of the entity type.</param>
        /// <param name="entityId">The entity identifier.</param>
        [LoggerMessage(5, LogLevel.Information, "[{LogKey}] repository: findone (type={EntityType}, id={EntityId})")]
        public static partial void LogFindOneId(ILogger logger, string logKey, string entityType, object entityId);

        /// <summary>
        /// Writes a log entry for the find one operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="entityType">The name of the entity type.</param>
        [LoggerMessage(6, LogLevel.Information, "[{LogKey}] repository: findone (type={EntityType})")]
        public static partial void LogFindOne(ILogger logger, string logKey, string entityType);
    }
}
