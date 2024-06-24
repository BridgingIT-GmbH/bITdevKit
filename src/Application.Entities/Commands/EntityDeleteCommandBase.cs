// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using FluentValidation;
using FluentValidation.Results;

public abstract class EntityDeleteCommandBase<TEntity>(string id, string identity = null)
    : CommandRequestBase<Result<EntityDeletedCommandResult>>,
    IEntityDeleteCommand<TEntity>
    where TEntity : class, IEntity
{
    private List<AbstractValidator<EntityDeleteCommandBase<TEntity>>> validators;

    public string EntityId { get; } = id;

    public TEntity Entity { get; set; }

    object IEntityDeleteCommand.Entity
    {
        get { return this.Entity; }
        set { this.Entity = (TEntity)value; }
    }

    public string Identity { get; } = identity;

    public EntityDeleteCommandBase<TEntity> AddValidator(
        AbstractValidator<EntityDeleteCommandBase<TEntity>> validator)
    {
        (this.validators ??= []).AddOrUpdate(validator);

        return this;
    }

    public EntityDeleteCommandBase<TEntity> AddValidator<TValidator>()
        where TValidator : class => this.AddValidator(
            Factory<TValidator>.Create() as AbstractValidator<EntityDeleteCommandBase<TEntity>>);

    public override ValidationResult Validate() =>
        new Validator(this.validators).Validate(this);

    public class Validator : AbstractValidator<EntityDeleteCommandBase<TEntity>>
    {
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
