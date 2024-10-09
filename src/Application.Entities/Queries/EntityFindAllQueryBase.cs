// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

public abstract class EntityFindAllQueryBase<TEntity>(
    int pageNumber = 1,
    int pageSize = int.MaxValue,
    string searchString = null,
    string orderBy = null,
    string include = null) : QueryRequestBase<PagedResult<TEntity>>, IEntityFindAllQuery<TEntity>
    where TEntity : class, IEntity
{
    private List<AbstractValidator<EntityFindAllQueryBase<TEntity>>> validators;

    protected EntityFindAllQueryBase()
        : this(1) { }

    public int PageNumber { get; set; } = pageNumber <= 0 ? 1 : pageNumber;

    public int PageSize { get; set; } = pageSize <= 0 ? int.MaxValue : pageSize;

    public string SearchString { get; set; } = searchString ?? string.Empty;

    public string OrderBy { get; set; } = orderBy;

    public string Include { get; set; } = include;

    public EntityFindAllQueryBase<TEntity> AddValidator(AbstractValidator<EntityFindAllQueryBase<TEntity>> validator)
    {
        (this.validators ??= []).AddOrUpdate(validator);

        return this;
    }

    public EntityFindAllQueryBase<TEntity> AddValidator<TValidator>()
        where TValidator : class
    {
        return this.AddValidator(Factory<TValidator>.Create() as AbstractValidator<EntityFindAllQueryBase<TEntity>>);
    }

    public override ValidationResult Validate()
    {
        return new Validator(this.validators).Validate(this);
    }

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