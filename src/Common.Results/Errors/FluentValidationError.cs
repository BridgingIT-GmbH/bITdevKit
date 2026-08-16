// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using FluentValidation.Results;

/// <summary>Represents the validation failures produced by FluentValidation.</summary>
/// <param name="validationResult">The validation result whose failures are retained by the error.</param>
public class FluentValidationError(ValidationResult validationResult)
    //: ResultErrorBase($"{string.Join(Environment.NewLine, validationResult.Errors.Select(e => e.ErrorMessage))}")
    : ResultErrorBase("Validation not satisfied, see errors for details")
{
    /// <summary>Gets the individual validation failures.</summary>
    public List<ValidationFailure> Errors { get; } = validationResult.Errors;

    /// <summary>Throws a FluentValidation exception containing the recorded failures.</summary>
    /// <exception cref="ValidationException">Always thrown with <see cref="Errors"/>.</exception>
    public override void Throw()
    {
        throw new ValidationException(this.Errors);
    }
}
