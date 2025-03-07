// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;

/// <summary>
/// Represents a single validation rule to be applied to a value.
/// </summary>
public class ValidationRule<T>(T instance, IValidator<T> validator) : RuleBase
{
    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public override string Message => "Rule validation not satisfied";

    /// <summary>
    /// Executes a specified rule.
    /// </summary>
    /// <param name="ruleId">Identifier of the rule to be executed.</param>
    /// <param name="parameters">Parameters required for the rule execution.</param>
    /// <returns>A boolean indicating if the rule execution was successful.</returns>
    public override Result Execute()
    {
        var validationResult = validator.Validate(instance);

        return validationResult.IsValid
            ? Result.Success()
            : Result.Failure().WithError(new FluentValidationError(validationResult));
    }
}