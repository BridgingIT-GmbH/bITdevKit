// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Represents entity create command base.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="entity">The entity involved in the operation.</param>
/// <param name="identity">The identity used by the operation.</param>
[Obsolete("Use the new Requester from now on")]
public abstract class EntityCreateCommandBase<TEntity>(TEntity entity, string identity = null)
    : CommandRequestBase<Result<EntityCreatedCommandResult>>, IEntityCreateCommand<TEntity>
    where TEntity : class, IEntity
{
    private List<AbstractValidator<EntityCreateCommandBase<TEntity>>> validators;

    /// <summary>
    /// Gets the entity.
    /// </summary>
    public TEntity Entity { get; } = entity;

    object IEntityCreateCommand.Entity => this.Entity;

    /// <summary>
    /// Gets the identity.
    /// </summary>
    public string Identity { get; } = identity;

    /// <summary>
    /// Adds validator.
    /// </summary>
    /// <param name="validator">The validator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public EntityCreateCommandBase<TEntity> AddValidator(AbstractValidator<EntityCreateCommandBase<TEntity>> validator)
    {
        (this.validators ??= []).AddOrUpdate(validator);

        return this;
    }

    /// <summary>
    /// Represents add validator.
    /// </summary>
    /// <typeparam name="TValidator">The validator type.</typeparam>
    public EntityCreateCommandBase<TEntity> AddValidator<TValidator>()
        where TValidator : class
    {
        return this.AddValidator(Factory<TValidator>.Create() as AbstractValidator<EntityCreateCommandBase<TEntity>>);
    }

    /// <inheritdoc/>
    public override ValidationResult Validate()
    {
        return new Validator(this.validators).Validate(this);
    }

    /// <summary>
    /// Represents validator.
    /// </summary>
    public class Validator : AbstractValidator<EntityCreateCommandBase<TEntity>>
    {
        /// <summary>
        /// Initializes a new instance of the <c>Validator</c> class.
        /// </summary>
        /// <param name="validators">The validators used by the operation.</param>
        public Validator(IEnumerable<AbstractValidator<EntityCreateCommandBase<TEntity>>> validators = null)
        {
            foreach (var validator in validators.SafeNull())
            {
                this.Include(validator); // https://docs.fluentvalidation.net/en/latest/including-rules.html
            }

            this.RuleFor(c => c.Entity)
                .NotNull()
                .NotEmpty()
                .ChildRules(c =>
                {
                    c.RuleFor(c => c.Id).Must(id => id.To<Guid>() == Guid.Empty).WithMessage("Invalid guid.");
                    // TODO: fluentvalidator message localization https://docs.fluentvalidation.net/en/latest/localization.html
                });
        }
    }
}
