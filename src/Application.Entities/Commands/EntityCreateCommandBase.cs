// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using System;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using FluentValidation;
using FluentValidation.Results;

public abstract class EntityCreateCommandBase<TEntity>(TEntity entity, string identity = null) :
    CommandRequestBase<Result<EntityCreatedCommandResult>>,
    IEntityCreateCommand<TEntity>
    where TEntity : class, IEntity
{
    private List<AbstractValidator<EntityCreateCommandBase<TEntity>>> validators;

    public TEntity Entity { get; } = entity;

    object IEntityCreateCommand.Entity
    {
        get { return this.Entity; }
    }

    public string Identity { get; } = identity;

    public EntityCreateCommandBase<TEntity> AddValidator(
        AbstractValidator<EntityCreateCommandBase<TEntity>> validator)
    {
        (this.validators ??= []).AddOrUpdate(validator);

        return this;
    }

    public EntityCreateCommandBase<TEntity> AddValidator<TValidator>()
        where TValidator : class => this.AddValidator(
            Factory<TValidator>.Create() as AbstractValidator<EntityCreateCommandBase<TEntity>>);

    public override ValidationResult Validate() =>
        new Validator(this.validators).Validate(this);

    public class Validator : AbstractValidator<EntityCreateCommandBase<TEntity>>
    {
        public Validator(IEnumerable<AbstractValidator<EntityCreateCommandBase<TEntity>>> validators = null)
        {
            foreach (var validator in validators.SafeNull())
            {
                this.Include(validator); // https://docs.fluentvalidation.net/en/latest/including-rules.html
            }

            this.RuleFor(c => c.Entity).NotNull().NotEmpty().ChildRules(c =>
            {
                c.RuleFor(c => c.Id).Must(id => id.To<Guid>() == Guid.Empty).WithMessage("Invalid guid.");
                // TODO: fluentvalidator message localization https://docs.fluentvalidation.net/en/latest/localization.html
            });
        }
    }
}
