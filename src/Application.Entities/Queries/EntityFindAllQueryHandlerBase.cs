// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Constants = BridgingIT.DevKit.Application.Queries.Constants;

public abstract class
    EntityFindAllQueryHandlerBase<TQuery, TEntity>
    : QueryHandlerBase<TQuery, ResultPaged<TEntity>> // TODO: move to FRAMEWORK Application.Queries
    where TQuery : EntityFindAllQueryBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly IGenericRepository<TEntity> repository;
    private List<ISpecification<TEntity>> specifications;
    private List<Func<TQuery, ISpecification<TEntity>>> specificationFuncs;

    protected EntityFindAllQueryHandlerBase(ILoggerFactory loggerFactory, IGenericRepository<TEntity> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public virtual EntityFindAllQueryHandlerBase<TQuery, TEntity> AddSpecification(
        ISpecification<TEntity> specification)
    {
        (this.specifications ??= []).AddOrUpdate(specification);

        return this;
    }

    public virtual EntityFindAllQueryHandlerBase<TQuery, TEntity> AddSpecification<TSpecification>()
        where TSpecification : class, ISpecification<TEntity>
    {
        return this.AddSpecification(Factory<TSpecification>.Create());
    }

    public virtual EntityFindAllQueryHandlerBase<TQuery, TEntity> AddSpecification(
        Func<TQuery, ISpecification<TEntity>> specification)
    {
        (this.specificationFuncs ??= []).AddOrUpdate(specification);

        return this;
    }

    public virtual IEnumerable<ISpecification<TEntity>> AddSpecifications(TQuery request)
    {
        return [];
    }

    public override async Task<QueryResponse<ResultPaged<TEntity>>> Process(
        TQuery query,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(query, nameof(query));

        var specifications = (this.specifications ??= []).Union(this.AddSpecifications(query).SafeNull()).ToList();
        this.specificationFuncs?.ForEach(s => specifications.Add(s.Invoke(query)));

        if (specifications.SafeAny())
        {
            this.Logger.LogDebug(
                $"{{LogKey}} entity specifications: {specifications.SafeNull().Select(b => b.GetType().PrettyName()).ToString(", ")}",
                Constants.LogKey);
        }

        var result = await this.repository.FindAllResultPagedAsync(specifications,
            query.OrderBy,
            query.PageNumber,
            query.PageSize,
            includePath: query.Include,
            cancellationToken: cancellationToken);

        return new QueryResponse<ResultPaged<TEntity>> { Result = result };
    }
}