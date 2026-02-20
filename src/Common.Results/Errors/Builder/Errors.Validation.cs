// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation.Results;

public static partial class Errors
{
    public static partial class Validation
    {
        /// <summary>Creates a <see cref="ValidationError"/> for general validation failures.</summary>
        public static ValidationError Error(string message = null, string propertyName = null, object attemptedValue = null)
            => new(message, propertyName, attemptedValue);

        /// <summary>Creates a <see cref="FluentValidationError"/> for FluentValidation failures.</summary>
        public static FluentValidationError FluentValidation(ValidationResult validationResult)
            => new(validationResult);

        /// <summary>Creates a <see cref="CollectionValidationError"/> for collection validation failures at a specific index.</summary>
        public static CollectionValidationError CollectionValidate(string message, int index, string propertyName = null, object attemptedValue = null)
            => new(message, index, propertyName, attemptedValue);

        /// <summary>Creates an <see cref="ArgumentError"/> for invalid arguments.</summary>
        public static ArgumentError Argument(string argument = null)
            => new(argument);

        /// <summary>Creates an <see cref="InvalidInputError"/> for invalid user input with field context.</summary>
        public static InvalidInputError InvalidInput(string message = null, string fieldName = null, object providedValue = null)
            => new(message, fieldName, providedValue);

        /// <summary>Creates an <see cref="InvalidFormatError"/> for format or parsing failures.</summary>
        public static InvalidFormatError InvalidFormat(string message = null, object receivedData = null)
            => new(message, receivedData);

        /// <summary>Creates a <see cref="RequiredFieldError"/> for missing required fields.</summary>
        public static RequiredFieldError RequiredField(string fieldName, string message = null)
            => new(fieldName, message);

        /// <summary>Creates a <see cref="DuplicateError"/> for duplicate values with context.</summary>
        public static DuplicateError Duplicate(string message = null, string propertyName = null, object attemptedValue = null)
            => new(message, propertyName, attemptedValue);

        /// <summary>Creates a <see cref="MappingError"/> for object mapping failures.</summary>
        public static MappingError Mapping(Exception exception, string message = null)
            => new(exception, message);
    }
}