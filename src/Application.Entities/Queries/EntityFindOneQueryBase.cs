// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Represents entity find one query base.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
[Obsolete("Use the new Requester from now on")]
public abstract class EntityFindOneQueryBase<TEntity> : QueryRequestBase<Result<TEntity>>, IEntityFindOneQuery<TEntity>
    where TEntity : class, IEntity
{
    private List<AbstractValidator<EntityFindOneQueryBase<TEntity>>> validators;

    /// <summary>
    /// Initializes a new instance of the <c>EntityFindOneQueryBase</c> class.
    /// </summary>
    /// <param name="entityId">The entity identifier.</param>
    protected EntityFindOneQueryBase(string entityId)
    {
        EnsureArg.IsNotNullOrEmpty(entityId, nameof(entityId));

        this.EntityId = entityId;
    }

    /// <summary>
    /// Gets the entity id.
    /// </summary>
    public string EntityId { get; }

    /// <summary>
    /// Adds validator.
    /// </summary>
    /// <param name="validator">The validator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public EntityFindOneQueryBase<TEntity> AddValidator(AbstractValidator<EntityFindOneQueryBase<TEntity>> validator)
    {
        (this.validators ??= []).AddOrUpdate(validator);

        return this;
    }

    /// <summary>
    /// Represents add validator.
    /// </summary>
    /// <typeparam name="TValidator">The validator type.</typeparam>
    public EntityFindOneQueryBase<TEntity> AddValidator<TValidator>()
        where TValidator : class
    {
        return this.AddValidator(Factory<TValidator>.Create() as AbstractValidator<EntityFindOneQueryBase<TEntity>>);
    }

    /// <inheritdoc/>
    public override ValidationResult Validate()
    {
        return new Validator(this.validators).Validate(this);
    }

    /// <summary>
    /// Represents validator.
    /// </summary>
    public class Validator : AbstractValidator<EntityFindOneQueryBase<TEntity>>
    {
        /// <summary>
        /// Initializes a new instance of the <c>Validator</c> class.
        /// </summary>
        /// <param name="validators">The validators used by the operation.</param>
        public Validator(IEnumerable<AbstractValidator<EntityFindOneQueryBase<TEntity>>> validators = null)
        {
            foreach (var validator in validators.SafeNull())
            {
                this.Include(validator); // https://docs.fluentvalidation.net/en/latest/including-rules.html
            }

            this.RuleFor(c => c.EntityId).Must(id => Guid.TryParse(id, out var idOut)).WithMessage("Invalid guid.");
        }
    }
}
