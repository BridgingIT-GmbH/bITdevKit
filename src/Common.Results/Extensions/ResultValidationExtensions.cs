// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using FluentValidation.Results;

/// <summary>
///     Extensions to integrate FluentValidation with Result types.
/// </summary>
public static class ResultValidationExtensions
{
    /// <summary>
    ///     Validates an object using a FluentValidation validator and returns a Result.
    /// </summary>
    /// <typeparam name="T">The type of the object to validate.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validator">The FluentValidation validator.</param>
    /// <returns>A Result containing validation details.</returns>
    /// <example>
    /// <code>
    /// var user = new User { Name = "John", Age = 25 };
    /// var result = user.Validate(new UserValidator());
    /// </code>
    /// </example>
    public static Result<T> Validate<T>(this T value, IValidator<T> validator)
    {
        if (validator is null)
        {
            return Result<T>.Failure(value)
                .WithError(new ValidationError("Validator cannot be null"));
        }

        try
        {
            var validationResult = validator.Validate(value);

            return validationResult.IsValid
                ? Result<T>.Success(value)
                : Result<T>.Failure(value)
                    .WithErrors(validationResult.Errors.Select(ToValidationError))
                    .WithMessage("Validation failed");
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(value)
                .WithError(new ExceptionError(ex))
                .WithMessage("Validation failed due to an error");
        }
    }

    /// <summary>
    ///     Validates a collection of objects using a FluentValidation validator.
    /// </summary>
    /// <typeparam name="T">The type of the objects to validate.</typeparam>
    /// <param name="values">The collection of values to validate.</param>
    /// <param name="validator">The FluentValidation validator.</param>
    /// <returns>A Result containing all values if validation passes, or all validation errors if it fails.</returns>
    /// <example>
    /// <code>
    /// var users = new[] {
    ///     new User { Name = "John", Age = 25 },
    ///     new User { Name = "Jane", Age = 30 }
    /// };
    /// var result = users.Validate(new UserValidator());
    /// </code>
    /// </example>
    public static Result<IEnumerable<T>> Validate<T>(this IEnumerable<T> values, IValidator<T> validator)
    {
        if (validator is null)
        {
            return Result<IEnumerable<T>>.Failure()
                .WithError(new ValidationError("Validator cannot be null"));
        }

        var valuesList = values?.ToList() ?? [];
        if (!valuesList.Any())
        {
            return Result<IEnumerable<T>>.Success(valuesList);
        }

        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var (value, index) in valuesList.Select((v, i) => (v, i)))
        {
            try
            {
                var validationResult = validator.Validate(value);
                if (!validationResult.IsValid)
                {
                    errors.AddRange(validationResult.Errors.Select(
                        error => ToCollectionValidationError(error, index)));
                }
            }
            catch (Exception ex)
            {
                errors.Add(new CollectionValidationError(
                    $"Validation failed for item at index {index}: {ex.Message}",
                    index));
                messages.Add(ex.Message);
            }
        }

        return errors.Any()
            ? Result<IEnumerable<T>>.Failure(valuesList)
                .WithErrors(errors)
                .WithMessages(messages)
                .WithMessage("Collection validation failed")
            : Result<IEnumerable<T>>.Success(valuesList);
    }

    /// <summary>
    ///     Validates a collection of objects using multiple FluentValidation validators.
    /// </summary>
    /// <typeparam name="T">The type of the objects to validate.</typeparam>
    /// <param name="values">The collection of values to validate.</param>
    /// <param name="validators">Collection of FluentValidation validators.</param>
    /// <returns>A Result containing all values if validation passes, or all validation errors if it fails.</returns>
    /// <example>
    /// <code>
    /// var users = GetUsers();
    /// var result = users.Validate(new[] {
    ///     new UserBaseValidator(),
    ///     new UserEmailValidator()
    /// });
    /// </code>
    /// </example>
    public static Result<IEnumerable<T>> Validate<T>(
        this IEnumerable<T> values,
        IEnumerable<IValidator<T>> validators)
    {
        var validatorsList = validators?.ToList();
        if (validatorsList?.Any() != true)
        {
            return Result<IEnumerable<T>>.Failure()
                .WithError(new ValidationError("No validators provided"));
        }

        var valuesList = values?.ToList() ?? [];
        if (!valuesList.Any())
        {
            return Result<IEnumerable<T>>.Success(valuesList);
        }

        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var (value, index) in valuesList.Select((v, i) => (v, i)))
        {
            foreach (var validator in validatorsList)
            {
                try
                {
                    var validationResult = validator.Validate(value);
                    if (!validationResult.IsValid)
                    {
                        errors.AddRange(validationResult.Errors.Select(
                            error => ToCollectionValidationError(error, index)));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new CollectionValidationError(
                        $"Validation failed for item at index {index}: {ex.Message}",
                        index));
                    messages.Add(ex.Message);
                }
            }
        }

        return errors.Any()
            ? Result<IEnumerable<T>>.Failure(valuesList)
                .WithErrors(errors)
                .WithMessages(messages)
                .WithMessage("Collection validation failed")
            : Result<IEnumerable<T>>.Success(valuesList);
    }

    /// <summary>
    ///     Ensures that a Result value satisfies a FluentValidation validator.
    /// </summary>
    /// <typeparam name="T">The type of the value to validate.</typeparam>
    /// <param name="result">The Result to validate.</param>
    /// <param name="validator">The FluentValidation validator.</param>
    /// <returns>A Result containing validation details.</returns>
    /// <example>
    /// <code>
    /// var result = GetUser(userId)
    ///     .EnsureValid(new UserValidator());
    /// </code>
    /// </example>
    public static Result<T> EnsureValid<T>(
        this Result<T> result,
        IValidator<T> validator)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        var validationResult = result.Value.Validate(validator);

        return validationResult.IsSuccess
            ? result
            : Result<T>.Failure(result.Value)
                .WithErrors(validationResult.Errors)
                .WithMessages(result.Messages)
                .WithMessage("Validation failed");
    }

    private static ValidationError ToValidationError(ValidationFailure failure)
    {
        return new ValidationError(
            failure.ErrorMessage,
            failure.PropertyName,
            failure.AttemptedValue);
    }

    private static CollectionValidationError ToCollectionValidationError(ValidationFailure failure, int index)
    {
        return new CollectionValidationError(
            failure.ErrorMessage,
            index,
            failure.PropertyName,
            failure.AttemptedValue);
    }
}