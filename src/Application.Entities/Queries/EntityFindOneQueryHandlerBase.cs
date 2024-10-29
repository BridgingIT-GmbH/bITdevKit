// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Constants = BridgingIT.DevKit.Application.Queries.Constants;

public class EntityFindOneQueryHandlerBase<TQuery, TEntity> : QueryHandlerBase<TQuery, Result<TEntity>>
    where TQuery : EntityFindOneQueryBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly IGenericRepository<TEntity> repository;
    private List<ISpecification<TEntity>> specifications;
    private List<Func<TQuery, ISpecification<TEntity>>> specificationFuncs;

    public EntityFindOneQueryHandlerBase(ILoggerFactory loggerFactory, IGenericRepository<TEntity> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));
        this.repository = repository;
    }

    public virtual EntityFindOneQueryHandlerBase<TQuery, TEntity> AddSpecification(
        ISpecification<TEntity> specification)
    {
        (this.specifications ??= []).AddOrUpdate(specification);

        return this;
    }

    public virtual EntityFindOneQueryHandlerBase<TQuery, TEntity> AddSpecification<TSpecification>()
        where TSpecification : class, ISpecification<TEntity>
    {
        return this.AddSpecification(Factory<TSpecification>.Create());
    }

    public virtual EntityFindOneQueryHandlerBase<TQuery, TEntity> AddSpecification(
        Func<TQuery, ISpecification<TEntity>> specification)
    {
        (this.specificationFuncs ??= []).AddOrUpdate(specification);

        return this;
    }

    public virtual IEnumerable<ISpecification<TEntity>> AddSpecifications(TQuery request)
    {
        return [];
    }

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