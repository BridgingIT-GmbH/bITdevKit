// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using FluentValidation;
using FluentValidation.Results;

public abstract class EntityFindAllQueryBase<TEntity> :
    QueryRequestBase<PagedResult<TEntity>>, IEntityFindAllQuery<TEntity>
    where TEntity : class, IEntity
{
    private List<AbstractValidator<EntityFindAllQueryBase<TEntity>>> validators;

    protected EntityFindAllQueryBase()
        : this(1, int.MaxValue, null, null)
    {
    }

    protected EntityFindAllQueryBase(int pageNumber = 1, int pageSize = int.MaxValue, string searchString = null, string orderBy = null, string include = null)
    {
        this.PageNumber = pageNumber <= 0 ? 1 : pageNumber;
        this.PageSize = pageSize <= 0 ? int.MaxValue : pageSize;
        this.SearchString = searchString ?? string.Empty;
        this.OrderBy = orderBy;
        this.Include = include;
    }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public string SearchString { get; set; }

    public string OrderBy { get; set; } // of the form fieldname [ascending|descending],fieldname [ascending|descending]...

    public string Include { get; set; }

    public EntityFindAllQueryBase<TEntity> AddValidator(
        AbstractValidator<EntityFindAllQueryBase<TEntity>> validator)
    {
        (this.validators ??= new()).AddOrUpdate(validator);

        return this;
    }

    public EntityFindAllQueryBase<TEntity> AddValidator<TValidator>()
        where TValidator : class => this.AddValidator(
            Factory<TValidator>.Create() as AbstractValidator<EntityFindAllQueryBase<TEntity>>);

    public override ValidationResult Validate() =>
        new Validator(this.validators).Validate(this);

    public class Validator : AbstractValidator<EntityFindAllQueryBase<TEntity>>
    {
        public Validator(IEnumerable<AbstractValidator<EntityFindAllQueryBase<TEntity>>> validators = null)
        {
            foreach (var validator in validators.SafeNull())
            {
                this.Include(validator); // https://docs.fluentvalidation.net/en/latest/including-rules.html
            }

            this.RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            this.RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);
        }
    }
}