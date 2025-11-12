// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;
using FluentValidation;
using FluentValidation.Results;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A behavior that validates entities before insert, update, or delete operations using DataAnnotations attributes.
/// </summary>
/// <typeparam name="TEntity">The entity type that inherits from <see cref="ActiveEntity{TEntity, TId}"/>.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ActiveEntityAnnotationsValidationBehavior{TEntity, TId}"/> class.
/// This behavior generates a FluentValidation validator from DataAnnotations attributes and caches it statically.
/// Supported attributes include [Required], [StringLength], [MaxLength], [MinLength], [Range], [RegularExpression], [EmailAddress], [Phone], [Url], and [Compare].
/// </remarks>
/// <param name="options">Configuration options specifying when validation applies.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
public class ActiveEntityAnnotationsValidationBehavior<TEntity, TId>(object options)
    : ActiveEntityBehaviorBase<TEntity>
    where TEntity : class, IEntity
{
    private static readonly ConcurrentDictionary<Type, IValidator> ValidatorCache = [];
    private readonly ActiveEntityValidatorBehaviorOptions options = (ActiveEntityValidatorBehaviorOptions)options ?? new ActiveEntityValidatorBehaviorOptions();

    /// <summary>
    /// Validates the entity before insertion using DataAnnotations if configured to do so.
    /// </summary>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Result{TEntity}"/> indicating success or failure with validation errors.</returns>
    public override async Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken)
    {
        if (this.options.ApplyOn is ApplyOn.Insert or ApplyOn.Upsert)
        {
            return await this.ValidateAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <summary>
    /// Validates the entity before update using DataAnnotations if configured to do so.
    /// </summary>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Result{TEntity}"/> indicating success or failure with validation errors.</returns>
    public override async Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        if (this.options.ApplyOn is ApplyOn.Update or ApplyOn.Upsert)
        {
            return await this.ValidateAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <summary>
    /// Validates the entity before deletion using DataAnnotations if configured to do so.
    /// </summary>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure with validation errors.</returns>
    public override async Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken)
    {
        if (this.options.ApplyOn is ApplyOn.Delete)
        {
            var validationResult = await this.ValidateAsync(entity, cancellationToken).ConfigureAwait(false);
            return validationResult.IsSuccess ? Result.Success() : Result.Failure().WithErrors(validationResult.Errors);
        }

        return Result.Success();
    }

    private async Task<Result<TEntity>> ValidateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        if (entity == null)
        {
            return Result<TEntity>.Failure()
                .WithError(new FluentValidationError(new FluentValidation.Results.ValidationResult(
                    [new ValidationFailure("Entity", "Entity cannot be null")])));
        }

        var validator = ValidatorCache.GetOrAdd(typeof(TEntity), _ => CreateInlineValidator());
        var context = new ValidationContext<TEntity>(entity);
        var result = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);

        if (!result.IsValid)
        {
            return Result<TEntity>.Failure()
                .WithError(new FluentValidationError(new FluentValidation.Results.ValidationResult(result.Errors)));
        }

        return Result<TEntity>.Success(entity);
    }

    /// <summary>
    ///  Maps data annotations attributes to FluentValidation rules.
    /// </summary>
    private static InlineValidator<TEntity> CreateInlineValidator()
    {
        var validator = new InlineValidator<TEntity>();

        foreach (var prop in typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var attr in prop.GetCustomAttributes<ValidationAttribute>(true))
            {
                //var errorMessage = attr.ErrorMessage ?? attr.GetValidationResult(
                //    prop.GetValue(Activator.CreateInstance<TEntity>()), new ValidationContext(null))?.ErrorMessage; // TODO: ValidationContext needs instance which is not applicable to cached and reused validators
                switch (attr)
                {
                    case RequiredAttribute _:
                        if (prop.PropertyType == typeof(string))
                        {
                            validator.RuleFor(x => prop.GetValue(x) as string)
                                .NotEmpty()
                                .WithMessage(/*errorMessage ?? */$"{prop.Name} is required.");
                        }
                        else
                        {
                            validator.RuleFor(x => prop.GetValue(x))
                                .NotNull()
                                .WithMessage(/*errorMessage ?? */$"{prop.Name} is required.");
                        }
                        break;
                    case StringLengthAttribute stringLength:
                        validator.RuleFor(x => prop.GetValue(x) as string)
                            .Length(stringLength.MinimumLength, stringLength.MaximumLength)
                            .WithMessage(/*errorMessage ?? */$"{prop.Name} must be between {stringLength.MinimumLength} and {stringLength.MaximumLength} characters.");
                        break;
                    case MaxLengthAttribute maxLength:
                        validator.RuleFor(x => prop.GetValue(x) as string)
                            .MaximumLength(maxLength.Length)
                            .WithMessage(/*errorMessage ?? */$"{prop.Name} must not exceed {maxLength.Length} characters.");
                        break;
                    case MinLengthAttribute minLength:
                        validator.RuleFor(x => prop.GetValue(x) as string)
                            .MinimumLength(minLength.Length)
                            .WithMessage(/*errorMessage ?? */$"{prop.Name} must be at least {minLength.Length} characters.");
                        break;
                    case RangeAttribute range:
                        if (typeof(IComparable).IsAssignableFrom(prop.PropertyType))
                        {
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception
                            try
                            {
                                if (Convert.ChangeType(range.Minimum, prop.PropertyType) is IComparable min && Convert.ChangeType(range.Maximum, prop.PropertyType) is IComparable max)
                                {
                                    validator.AddRangeRule(prop, min, max, $"{prop.Name} must be between {range.Minimum} and {range.Maximum}.");
                                }
                            }
                            catch (Exception)
                            {
                                // Skip if conversion fails (e.g., mismatched types)
                            }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception
                        }
                        break;
                    case RegularExpressionAttribute regex:
                        validator.RuleFor(x => prop.GetValue(x) as string)
                            .Matches(regex.Pattern)
                            .WithMessage(/*errorMessage ?? */$"{prop.Name} does not match the required pattern.");
                        break;
                    case EmailAddressAttribute _:
                        validator.RuleFor(x => prop.GetValue(x) as string)
                            .EmailAddress()
                            .WithMessage(/*errorMessage ?? */$"{prop.Name} must be a valid email address.");
                        break;
                    case PhoneAttribute _:
                        validator.RuleFor(x => prop.GetValue(x) as string)
                            .Matches(@"^\+?1?\d{9,15}$")
                            .WithMessage(/*errorMessage ?? */$"{prop.Name} must be a valid phone number.");
                        break;
                    case UrlAttribute _:
                        validator.RuleFor(x => prop.GetValue(x) as string)
                            .Matches(@"^(https?|ftp):\/\/[^\s/$.?#].[^\s]*$")
                            .WithMessage(/*errorMessage ?? */$"{prop.Name} must be a valid URL.");
                        break;
                    case CompareAttribute compare:
                        validator.RuleFor(x => prop.GetValue(x))
                            .Equal(x => typeof(TEntity).GetProperty(compare.OtherProperty).GetValue(x))
                            .WithMessage(/*errorMessage ?? */$"{prop.Name} must match {compare.OtherProperty}.");
                        break;
                }
            }
        }

        return validator;
    }
}