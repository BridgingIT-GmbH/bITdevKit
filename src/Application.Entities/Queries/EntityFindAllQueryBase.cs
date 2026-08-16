// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Represents entity find all query base.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="pageNumber">The page number used by the operation.</param>
/// <param name="pageSize">The page size used by the operation.</param>
/// <param name="searchString">The search string used by the operation.</param>
/// <param name="orderBy">The order by used by the operation.</param>
/// <param name="include">The include used by the operation.</param>
[Obsolete("Use the new Requester from now on")]
public abstract class EntityFindAllQueryBase<TEntity>(
    int pageNumber = 1,
    int pageSize = int.MaxValue,
    string searchString = null,
    string orderBy = null,
    string include = null) : QueryRequestBase<ResultPaged<TEntity>>, IEntityFindAllQuery<TEntity>
    where TEntity : class, IEntity
{
    private List<AbstractValidator<EntityFindAllQueryBase<TEntity>>> validators;

    /// <summary>
    /// Initializes a new instance of the <c>EntityFindAllQueryBase</c> class.
    /// </summary>
    protected EntityFindAllQueryBase()
        : this(1) { }

    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int PageNumber { get; set; } = pageNumber <= 0 ? 1 : pageNumber;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = pageSize <= 0 ? int.MaxValue : pageSize;

    /// <summary>
    /// Gets or sets the search string.
    /// </summary>
    public string SearchString { get; set; } = searchString ?? string.Empty;

    /// <summary>
    /// Gets or sets the order by.
    /// </summary>
    public string OrderBy { get; set; } = orderBy;

    /// <summary>
    /// Gets or sets the include.
    /// </summary>
    public string Include { get; set; } = include;

    /// <summary>
    /// Adds validator.
    /// </summary>
    /// <param name="validator">The validator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public EntityFindAllQueryBase<TEntity> AddValidator(AbstractValidator<EntityFindAllQueryBase<TEntity>> validator)
    {
        (this.validators ??= []).AddOrUpdate(validator);

        return this;
    }

    /// <summary>
    /// Represents add validator.
    /// </summary>
    /// <typeparam name="TValidator">The validator type.</typeparam>
    public EntityFindAllQueryBase<TEntity> AddValidator<TValidator>()
        where TValidator : class
    {
        return this.AddValidator(Factory<TValidator>.Create() as AbstractValidator<EntityFindAllQueryBase<TEntity>>);
    }

    /// <inheritdoc/>
    public override ValidationResult Validate()
    {
        return new Validator(this.validators).Validate(this);
    }

    /// <summary>
    /// Represents validator.
    /// </summary>
    public class Validator : AbstractValidator<EntityFindAllQueryBase<TEntity>>
    {
        /// <summary>
        /// Initializes a new instance of the <c>Validator</c> class.
        /// </summary>
        /// <param name="validators">The validators used by the operation.</param>
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
