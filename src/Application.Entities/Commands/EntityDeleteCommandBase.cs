// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Represents entity delete command base.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="id">The entity identifier.</param>
/// <param name="identity">The identity used by the operation.</param>
[Obsolete("Use the new Requester from now on")]
public abstract class EntityDeleteCommandBase<TEntity>(string id, string identity = null)
    : CommandRequestBase<Result<EntityDeletedCommandResult>>, IEntityDeleteCommand<TEntity>
    where TEntity : class, IEntity
{
    private List<AbstractValidator<EntityDeleteCommandBase<TEntity>>> validators;

    /// <summary>
    /// Gets the entity id.
    /// </summary>
    public string EntityId { get; } = id;

    /// <summary>
    /// Gets or sets the entity.
    /// </summary>
    public TEntity Entity { get; set; }

    object IEntityDeleteCommand.Entity
    {
        get => this.Entity;
        set => this.Entity = (TEntity)value;
    }

    /// <summary>
    /// Gets the identity.
    /// </summary>
    public string Identity { get; } = identity;

    /// <summary>
    /// Adds validator.
    /// </summary>
    /// <param name="validator">The validator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public EntityDeleteCommandBase<TEntity> AddValidator(AbstractValidator<EntityDeleteCommandBase<TEntity>> validator)
    {
        (this.validators ??= []).AddOrUpdate(validator);

        return this;
    }

    /// <summary>
    /// Represents add validator.
    /// </summary>
    /// <typeparam name="TValidator">The validator type.</typeparam>
    public EntityDeleteCommandBase<TEntity> AddValidator<TValidator>()
        where TValidator : class
    {
        return this.AddValidator(Factory<TValidator>.Create() as AbstractValidator<EntityDeleteCommandBase<TEntity>>);
    }

    /// <inheritdoc/>
    public override ValidationResult Validate()
    {
        return new Validator(this.validators).Validate(this);
    }

    /// <summary>
    /// Represents validator.
    /// </summary>
    public class Validator : AbstractValidator<EntityDeleteCommandBase<TEntity>>
    {
        /// <summary>
        /// Initializes a new instance of the <c>Validator</c> class.
        /// </summary>
        /// <param name="validators">The validators used by the operation.</param>
        public Validator(IEnumerable<AbstractValidator<EntityDeleteCommandBase<TEntity>>> validators = null)
        {
            foreach (var validator in validators.SafeNull())
            {
                this.Include(validator); // https://docs.fluentvalidation.net/en/latest/including-rules.html
            }

            this.RuleFor(c => c.EntityId).NotNull().NotEmpty();
            // TODO: fluentvalidator message localization https://docs.fluentvalidation.net/en/latest/localization.html
        }
    }
}
