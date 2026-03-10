// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Adds validation rules for import operations.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
public sealed class DataPorterValidationAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataPorterValidationAttribute"/> class.
    /// </summary>
    /// <param name="type">The validation type.</param>
    /// <param name="errorMessage">The custom error message.</param>
    public DataPorterValidationAttribute(ValidationType type, string errorMessage = null)
    {
        this.Type = type;
        this.ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets the validation type.
    /// </summary>
    public ValidationType Type { get; }

    /// <summary>
    /// Gets or sets the custom error message.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the validation parameter (e.g., min length, max length, regex pattern).
    /// </summary>
    public object Parameter { get; set; }
}

/// <summary>
/// Specifies the type of validation to perform.
/// </summary>
public enum ValidationType
{
    /// <summary>
    /// The field is required and cannot be null or empty.
    /// </summary>
    Required,

    /// <summary>
    /// The field must have a minimum length. Parameter: int (minimum length).
    /// </summary>
    MinLength,

    /// <summary>
    /// The field must not exceed a maximum length. Parameter: int (maximum length).
    /// </summary>
    MaxLength,

    /// <summary>
    /// The field must be within a range. Parameter: string "min,max".
    /// </summary>
    Range,

    /// <summary>
    /// The field must match a regular expression. Parameter: string (regex pattern).
    /// </summary>
    Regex,

    /// <summary>
    /// The field must be a valid email address.
    /// </summary>
    Email,

    /// <summary>
    /// The field must be a valid URL.
    /// </summary>
    Url,

    /// <summary>
    /// Custom validation using a validator type. Parameter: Type (validator type).
    /// </summary>
    Custom
}
