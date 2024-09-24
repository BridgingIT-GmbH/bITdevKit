// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Common;
using Domain.Model;
using FluentValidation;
using FluentValidation.Results;
using Queries;

public abstract class EntityFindOneQueryBase<TEntity> : QueryRequestBase<Result<TEntity>>, IEntityFindOneQuery<TEntity>
    where TEntity : class, IEntity
{
    private List<AbstractValidator<EntityFindOneQueryBase<TEntity>>> validators;

    protected EntityFindOneQueryBase(string entityId)
    {
        EnsureArg.IsNotNullOrEmpty(entityId, nameof(entityId));

        this.EntityId = entityId;
    }

    public string EntityId { get; }

    public EntityFindOneQueryBase<TEntity> AddValidator(AbstractValidator<EntityFindOneQueryBase<TEntity>> validator)
    {
        (this.validators ??= []).AddOrUpdate(validator);

        return this;
    }

    public EntityFindOneQueryBase<TEntity> AddValidator<TValidator>()
        where TValidator : class
    {
        return this.AddValidator(Factory<TValidator>.Create() as AbstractValidator<EntityFindOneQueryBase<TEntity>>);
    }

    public override ValidationResult Validate()
    {
        return new Validator(this.validators).Validate(this);
    }

    public class Validator : AbstractValidator<EntityFindOneQueryBase<TEntity>>
    {
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