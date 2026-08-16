// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Constants = Queries.Constants;

/// <summary>
/// Represents entity find one query handler base.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
[Obsolete("Use the new Requester from now on")]
public class EntityFindOneQueryHandlerBase<TQuery, TEntity> : QueryHandlerBase<TQuery, Result<TEntity>>
    where TQuery : EntityFindOneQueryBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly IGenericRepository<TEntity> repository;
    private List<ISpecification<TEntity>> specifications;
    private List<Func<TQuery, ISpecification<TEntity>>> specificationFuncs;

    /// <summary>
    /// Initializes a new instance of the <c>EntityFindOneQueryHandlerBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="repository">The repository used by the operation.</param>
    public EntityFindOneQueryHandlerBase(ILoggerFactory loggerFactory, IGenericRepository<TEntity> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));
        this.repository = repository;
    }

    /// <summary>
    /// Adds specification.
    /// </summary>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <returns>The result of the operation.</returns>
    public virtual EntityFindOneQueryHandlerBase<TQuery, TEntity> AddSpecification(
        ISpecification<TEntity> specification)
    {
        (this.specifications ??= []).AddOrUpdate(specification);

        return this;
    }

    /// <summary>
    /// Represents add specification.
    /// </summary>
    /// <typeparam name="TSpecification">The specification type.</typeparam>
    public virtual EntityFindOneQueryHandlerBase<TQuery, TEntity> AddSpecification<TSpecification>()
        where TSpecification : class, ISpecification<TEntity>
    {
        return this.AddSpecification(Factory<TSpecification>.Create());
    }

    /// <summary>
    /// Adds specification.
    /// </summary>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <returns>The result of the operation.</returns>
    public virtual EntityFindOneQueryHandlerBase<TQuery, TEntity> AddSpecification(
        Func<TQuery, ISpecification<TEntity>> specification)
    {
        (this.specificationFuncs ??= []).AddOrUpdate(specification);

        return this;
    }

    /// <summary>
    /// Adds specifications.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual IEnumerable<ISpecification<TEntity>> AddSpecifications(TQuery request)
    {
        return [];
    }

    /// <inheritdoc/>
    public override async Task<QueryResponse<Result<TEntity>>> Process(
        TQuery query,
        CancellationToken cancellationToken)
    {
        var entity = await this.repository.FindOneAsync(Guid.Parse(query.EntityId),
                cancellationToken: cancellationToken)
            .AnyContext();

        var specifications = (this.specifications ??= []).Union(this.AddSpecifications(query).SafeNull()).ToList();
        this.specificationFuncs?.ForEach(s => specifications.Add(s.Invoke(query)));

        if (specifications.SafeAny())
        {
            this.Logger.LogDebug(
                $"{{LogKey}} entity specifications: {specifications.SafeNull().Select(b => b.GetType().PrettyName()).ToString(", ")}",
                Constants.LogKey);
        }

        if (SpecificationExtensions.IsSatisfiedBy(specifications, entity))
        {
            return new QueryResponse<Result<TEntity>> { Result = Result<TEntity>.Success(entity) };
        }

        return new QueryResponse<Result<TEntity>>
        {
            Result = Result<TEntity>.Failure("NotFound").WithError<NotFoundError>()
        };
    }
}
