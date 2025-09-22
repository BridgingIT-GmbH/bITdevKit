// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A behavior that validates entities before insert, update, or delete operations using FluentValidation.
/// </summary>
/// <typeparam name="TEntity">The entity type that inherits from <see cref="ActiveEntity{TEntity, TId}"/>.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ActiveEntityValidationBehavior{TEntity, TId}"/> class.
/// This behavior is registered once per entity type and manages all applicable validators internally.
/// </remarks>
/// <param name="serviceProvider">The service provider to resolve validator registrations.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
public class ActiveEntityValidationBehavior<TEntity, TId>(IServiceProvider serviceProvider)
    : ActiveEntityBehaviorBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Validates the entity before insertion using all registered validators configured for Insert or Upsert.
    /// </summary>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure with validation errors.</returns>
    public override async Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken)
    {
        return await this.ValidateAsync(entity, ApplyOn.Insert, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates the entity before update using all registered validators configured for Update or Upsert.
    /// </summary>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure with validation errors.</returns>
    public override async Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        return await this.ValidateAsync(entity, ApplyOn.Update, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates the entity before upsert using all registered validators configured for Upsert.
    /// </summary>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure with validation errors.</returns>
    public override async Task<Result> BeforeUpsertAsync(TEntity entity, CancellationToken cancellationToken)
    {
        return await this.ValidateAsync(entity, ApplyOn.Upsert, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates the entity before deletion using all registered validators configured for Delete.
    /// </summary>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure with validation errors.</returns>
    public override async Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken)
    {
        var validationResult = await this.ValidateAsync(entity, ApplyOn.Delete, cancellationToken).ConfigureAwait(false);
        return validationResult.IsSuccess ? Result.Success() : Result.Failure().WithErrors(validationResult.Errors);
    }

    private async Task<Result<TEntity>> ValidateAsync(TEntity entity, ApplyOn currentOperation, CancellationToken cancellationToken)
    {
        if (entity == null)
        {
            return Result<TEntity>.Failure()
                .WithError(new FluentValidationError(new ValidationResult(
                    [new ValidationFailure("Entity", "Entity cannot be null")])));
        }

        var validatorRegistrations = this.serviceProvider.GetServices<ValidatorRegistration<TEntity>>();
        var context = new ValidationContext<TEntity>(entity);
        var failures = new List<ValidationFailure>();

        foreach (var registration in validatorRegistrations.SafeNull().Distinct())
        {
            if (registration.ApplyOn == currentOperation ||
                (registration.ApplyOn == ApplyOn.Upsert && (currentOperation == ApplyOn.Insert || currentOperation == ApplyOn.Update)))
            {
                var result = await registration.Validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
                if (!result.IsValid)
                {
                    failures.AddRange(result.Errors);
                }
            }
        }

        if (failures.Count != 0)
        {
            return Result<TEntity>.Failure()
                .WithError(new FluentValidationError(new ValidationResult(failures)));
        }

        return Result<TEntity>.Success(entity);
    }
}

/// <summary>
/// Represents a validator registration with its associated operation type.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ValidatorRegistration{TEntity}"/> class.
/// </remarks>
/// <param name="validator">The validator instance.</param>
/// <param name="applyOn">The operation type.</param>
public class ValidatorRegistration<TEntity>(IValidator<TEntity> validator, ApplyOn applyOn)
{
    /// <summary>
    /// Gets the validator instance.
    /// </summary>
    public IValidator<TEntity> Validator { get; } = validator;

    /// <summary>
    /// Gets the operation type for which the validator is configured.
    /// </summary>
    public ApplyOn ApplyOn { get; } = applyOn;
}

/// <summary>
/// Specifies the operations on which validation should be applied.
/// </summary>
public enum ApplyOn
{
    /// <summary>
    /// Validation applies to insert operations.
    /// </summary>
    Insert,

    /// <summary>
    /// Validation applies to update operations.
    /// </summary>
    Update,

    /// <summary>
    /// Validation applies to delete operations.
    /// </summary>
    Delete,

    /// <summary>
    /// Validation applies to insert and update operations.
    /// </summary>
    Upsert
}