// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;

/// <summary>
/// Represents a single validation rule to be applied to a value.
/// </summary>
public class ValidationRule<T>(T instance, IValidator<T> validator, string message = null) : RuleBase
{
    private readonly string message = message ?? "Rule validation not satisfied";

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public override string Message => this.message;

    /// <summary>
    /// Validates the configured instance with the configured FluentValidation validator.
    /// </summary>
    /// <returns>A successful result when validation succeeds; otherwise, a failure result containing a <see cref="FluentValidationError"/>.</returns>
    public override Result Execute()
    {
        var validationResult = validator.Validate(instance);

        return validationResult.IsValid
            ? Result.Success()
            : Result.Failure().WithError(new FluentValidationError(validationResult));
    }
}