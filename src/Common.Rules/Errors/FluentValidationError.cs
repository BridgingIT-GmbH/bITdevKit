// // MIT-License
// // Copyright BridgingIT GmbH - All Rights Reserved
// // Use of this source code is governed by an MIT-style license that can be
// // found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
//
// namespace BridgingIT.DevKit.Common;
//
// using FluentValidation;
// using FluentValidation.Results;
//
// public class FluentValidationError(IRule rule, ValidationResult validationResult)
//     : ResultErrorBase($"{rule.Message}{Environment.NewLine}{string.Join(Environment.NewLine, validationResult.Errors.Select(e => e.ErrorMessage))}")
// {
//     public List<ValidationFailure> Errors { get; } = validationResult.Errors;
//
//     public override void Throw()
//     {
//         throw new ValidationException(this.Errors);
//     }
// }