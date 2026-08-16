// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Provides the optional error message shared by source-generated validation attributes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ValidationCodeGenAttribute"/> class.
/// </remarks>
/// <param name="message">The optional custom validation message.</param>
public abstract class ValidationCodeGenAttribute(string message = null) : Attribute
{
    /// <summary>
    /// Gets the optional custom validation message.
    /// </summary>
    public string Message { get; } = message;
}

/// <summary>
/// Provides a shared string-backed comparison value for source-generated validation attributes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SingleValueValidationCodeGenAttribute"/> class.
/// </remarks>
/// <param name="value">The invariant string comparison value.</param>
/// <param name="message">The optional custom validation message.</param>
public abstract class SingleValueValidationCodeGenAttribute(string value, string message = null) : ValidationCodeGenAttribute(message)
{
    /// <summary>
    /// Gets the invariant string comparison value.
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// Formats a numeric attribute argument using invariant culture.
    /// </summary>
    /// <param name="value">The numeric value to format.</param>
    /// <returns>The formatted invariant string.</returns>
    public static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a numeric attribute argument using invariant culture.
    /// </summary>
    /// <param name="value">The numeric value to format.</param>
    /// <returns>The formatted invariant string.</returns>
    public static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>NotNull</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateNotNullAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateNotNullAttribute"/> class.
    /// </summary>
    public ValidateNotNullAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateNotNullAttribute"/> class.
    /// </summary>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateNotNullAttribute(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>NotEmpty</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateNotEmptyAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateNotEmptyAttribute"/> class.
    /// </summary>
    public ValidateNotEmptyAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateNotEmptyAttribute"/> class.
    /// </summary>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateNotEmptyAttribute(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>Empty</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateEmptyAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateEmptyAttribute"/> class.
    /// </summary>
    public ValidateEmptyAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateEmptyAttribute"/> class.
    /// </summary>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateEmptyAttribute(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>Length</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateLengthAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateLengthAttribute"/> class.
    /// </summary>
    /// <param name="minimumLength">The inclusive minimum length.</param>
    /// <param name="maximumLength">The inclusive maximum length.</param>
    public ValidateLengthAttribute(int minimumLength, int maximumLength)
    {
        this.MinimumLength = minimumLength;
        this.MaximumLength = maximumLength;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateLengthAttribute"/> class.
    /// </summary>
    /// <param name="minimumLength">The inclusive minimum length.</param>
    /// <param name="maximumLength">The inclusive maximum length.</param>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateLengthAttribute(int minimumLength, int maximumLength, string message)
        : base(message)
    {
        this.MinimumLength = minimumLength;
        this.MaximumLength = maximumLength;
    }

    /// <summary>
    /// Gets the inclusive minimum length.
    /// </summary>
    public int MinimumLength { get; }

    /// <summary>
    /// Gets the inclusive maximum length.
    /// </summary>
    public int MaximumLength { get; }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>MinimumLength</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateMinLengthAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateMinLengthAttribute"/> class.
    /// </summary>
    /// <param name="length">The inclusive minimum length.</param>
    public ValidateMinLengthAttribute(int length) => this.Length = length;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateMinLengthAttribute"/> class.
    /// </summary>
    /// <param name="length">The inclusive minimum length.</param>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateMinLengthAttribute(int length, string message)
        : base(message) => this.Length = length;

    /// <summary>
    /// Gets the inclusive minimum length.
    /// </summary>
    public int Length { get; }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>MaximumLength</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateMaxLengthAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateMaxLengthAttribute"/> class.
    /// </summary>
    /// <param name="length">The inclusive maximum length.</param>
    public ValidateMaxLengthAttribute(int length) => this.Length = length;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateMaxLengthAttribute"/> class.
    /// </summary>
    /// <param name="length">The inclusive maximum length.</param>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateMaxLengthAttribute(int length, string message)
        : base(message) => this.Length = length;

    /// <summary>
    /// Gets the inclusive maximum length.
    /// </summary>
    public int Length { get; }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>GreaterThan</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateGreaterThanAttribute : SingleValueValidationCodeGenAttribute
{
    /// <summary>Initializes a greater-than rule with an integer comparison value.</summary>
    /// <param name="value">The exclusive lower bound.</param>
    public ValidateGreaterThanAttribute(long value) : base(Format(value)) { }
    /// <summary>Initializes a greater-than rule with an integer comparison value and message.</summary>
    /// <param name="value">The exclusive lower bound.</param><param name="message">The custom validation message.</param>
    public ValidateGreaterThanAttribute(long value, string message) : base(Format(value), message) { }
    /// <summary>Initializes a greater-than rule with a floating-point comparison value.</summary>
    /// <param name="value">The exclusive lower bound.</param>
    public ValidateGreaterThanAttribute(double value) : base(Format(value)) { }
    /// <summary>Initializes a greater-than rule with a floating-point comparison value and message.</summary>
    /// <param name="value">The exclusive lower bound.</param><param name="message">The custom validation message.</param>
    public ValidateGreaterThanAttribute(double value, string message) : base(Format(value), message) { }
    /// <summary>Initializes a greater-than rule with an invariant string comparison value.</summary>
    /// <param name="value">The exclusive lower bound.</param>
    public ValidateGreaterThanAttribute(string value) : base(value) { }
    /// <summary>Initializes a greater-than rule with an invariant string comparison value and message.</summary>
    /// <param name="value">The exclusive lower bound.</param><param name="message">The custom validation message.</param>
    public ValidateGreaterThanAttribute(string value, string message) : base(value, message) { }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>GreaterThanOrEqualTo</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateGreaterThanOrEqualToAttribute : SingleValueValidationCodeGenAttribute
{
    /// <summary>Initializes a greater-than-or-equal rule with an integer comparison value.</summary>
    /// <param name="value">The inclusive lower bound.</param>
    public ValidateGreaterThanOrEqualToAttribute(long value) : base(Format(value)) { }
    /// <summary>Initializes a greater-than-or-equal rule with an integer comparison value and message.</summary>
    /// <param name="value">The inclusive lower bound.</param><param name="message">The custom validation message.</param>
    public ValidateGreaterThanOrEqualToAttribute(long value, string message) : base(Format(value), message) { }
    /// <summary>Initializes a greater-than-or-equal rule with a floating-point comparison value.</summary>
    /// <param name="value">The inclusive lower bound.</param>
    public ValidateGreaterThanOrEqualToAttribute(double value) : base(Format(value)) { }
    /// <summary>Initializes a greater-than-or-equal rule with a floating-point comparison value and message.</summary>
    /// <param name="value">The inclusive lower bound.</param><param name="message">The custom validation message.</param>
    public ValidateGreaterThanOrEqualToAttribute(double value, string message) : base(Format(value), message) { }
    /// <summary>Initializes a greater-than-or-equal rule with an invariant string comparison value.</summary>
    /// <param name="value">The inclusive lower bound.</param>
    public ValidateGreaterThanOrEqualToAttribute(string value) : base(value) { }
    /// <summary>Initializes a greater-than-or-equal rule with an invariant string comparison value and message.</summary>
    /// <param name="value">The inclusive lower bound.</param><param name="message">The custom validation message.</param>
    public ValidateGreaterThanOrEqualToAttribute(string value, string message) : base(value, message) { }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>LessThan</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateLessThanAttribute : SingleValueValidationCodeGenAttribute
{
    /// <summary>Initializes a less-than rule with an integer comparison value.</summary>
    /// <param name="value">The exclusive upper bound.</param>
    public ValidateLessThanAttribute(long value) : base(Format(value)) { }
    /// <summary>Initializes a less-than rule with an integer comparison value and message.</summary>
    /// <param name="value">The exclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateLessThanAttribute(long value, string message) : base(Format(value), message) { }
    /// <summary>Initializes a less-than rule with a floating-point comparison value.</summary>
    /// <param name="value">The exclusive upper bound.</param>
    public ValidateLessThanAttribute(double value) : base(Format(value)) { }
    /// <summary>Initializes a less-than rule with a floating-point comparison value and message.</summary>
    /// <param name="value">The exclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateLessThanAttribute(double value, string message) : base(Format(value), message) { }
    /// <summary>Initializes a less-than rule with an invariant string comparison value.</summary>
    /// <param name="value">The exclusive upper bound.</param>
    public ValidateLessThanAttribute(string value) : base(value) { }
    /// <summary>Initializes a less-than rule with an invariant string comparison value and message.</summary>
    /// <param name="value">The exclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateLessThanAttribute(string value, string message) : base(value, message) { }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>LessThanOrEqualTo</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateLessThanOrEqualToAttribute : SingleValueValidationCodeGenAttribute
{
    /// <summary>Initializes a less-than-or-equal rule with an integer comparison value.</summary>
    /// <param name="value">The inclusive upper bound.</param>
    public ValidateLessThanOrEqualToAttribute(long value) : base(Format(value)) { }
    /// <summary>Initializes a less-than-or-equal rule with an integer comparison value and message.</summary>
    /// <param name="value">The inclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateLessThanOrEqualToAttribute(long value, string message) : base(Format(value), message) { }
    /// <summary>Initializes a less-than-or-equal rule with a floating-point comparison value.</summary>
    /// <param name="value">The inclusive upper bound.</param>
    public ValidateLessThanOrEqualToAttribute(double value) : base(Format(value)) { }
    /// <summary>Initializes a less-than-or-equal rule with a floating-point comparison value and message.</summary>
    /// <param name="value">The inclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateLessThanOrEqualToAttribute(double value, string message) : base(Format(value), message) { }
    /// <summary>Initializes a less-than-or-equal rule with an invariant string comparison value.</summary>
    /// <param name="value">The inclusive upper bound.</param>
    public ValidateLessThanOrEqualToAttribute(string value) : base(value) { }
    /// <summary>Initializes a less-than-or-equal rule with an invariant string comparison value and message.</summary>
    /// <param name="value">The inclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateLessThanOrEqualToAttribute(string value, string message) : base(value, message) { }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>Equal</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateEqualAttribute : SingleValueValidationCodeGenAttribute
{
    /// <summary>Initializes an equality rule with an integer comparison value.</summary>
    /// <param name="value">The required value.</param>
    public ValidateEqualAttribute(long value) : base(Format(value)) { }
    /// <summary>Initializes an equality rule with an integer comparison value and message.</summary>
    /// <param name="value">The required value.</param><param name="message">The custom validation message.</param>
    public ValidateEqualAttribute(long value, string message) : base(Format(value), message) { }
    /// <summary>Initializes an equality rule with a floating-point comparison value.</summary>
    /// <param name="value">The required value.</param>
    public ValidateEqualAttribute(double value) : base(Format(value)) { }
    /// <summary>Initializes an equality rule with a floating-point comparison value and message.</summary>
    /// <param name="value">The required value.</param><param name="message">The custom validation message.</param>
    public ValidateEqualAttribute(double value, string message) : base(Format(value), message) { }
    /// <summary>Initializes an equality rule with an invariant string comparison value.</summary>
    /// <param name="value">The required value.</param>
    public ValidateEqualAttribute(string value) : base(value) { }
    /// <summary>Initializes an equality rule with an invariant string comparison value and message.</summary>
    /// <param name="value">The required value.</param><param name="message">The custom validation message.</param>
    public ValidateEqualAttribute(string value, string message) : base(value, message) { }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>NotEqual</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateNotEqualAttribute : SingleValueValidationCodeGenAttribute
{
    /// <summary>Initializes an inequality rule with an integer comparison value.</summary>
    /// <param name="value">The disallowed value.</param>
    public ValidateNotEqualAttribute(long value) : base(Format(value)) { }
    /// <summary>Initializes an inequality rule with an integer comparison value and message.</summary>
    /// <param name="value">The disallowed value.</param><param name="message">The custom validation message.</param>
    public ValidateNotEqualAttribute(long value, string message) : base(Format(value), message) { }
    /// <summary>Initializes an inequality rule with a floating-point comparison value.</summary>
    /// <param name="value">The disallowed value.</param>
    public ValidateNotEqualAttribute(double value) : base(Format(value)) { }
    /// <summary>Initializes an inequality rule with a floating-point comparison value and message.</summary>
    /// <param name="value">The disallowed value.</param><param name="message">The custom validation message.</param>
    public ValidateNotEqualAttribute(double value, string message) : base(Format(value), message) { }
    /// <summary>Initializes an inequality rule with an invariant string comparison value.</summary>
    /// <param name="value">The disallowed value.</param>
    public ValidateNotEqualAttribute(string value) : base(value) { }
    /// <summary>Initializes an inequality rule with an invariant string comparison value and message.</summary>
    /// <param name="value">The disallowed value.</param><param name="message">The custom validation message.</param>
    public ValidateNotEqualAttribute(string value, string message) : base(value, message) { }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>InclusiveBetween</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateInclusiveBetweenAttribute : ValidationCodeGenAttribute
{
    /// <summary>Initializes an inclusive range rule with integer bounds.</summary>
    /// <param name="from">The inclusive lower bound.</param><param name="to">The inclusive upper bound.</param>
    public ValidateInclusiveBetweenAttribute(long from, long to)
    {
        this.From = SingleValueValidationCodeGenAttribute.Format(from);
        this.To = SingleValueValidationCodeGenAttribute.Format(to);
    }

    /// <summary>Initializes an inclusive range rule with integer bounds and a custom message.</summary>
    /// <param name="from">The inclusive lower bound.</param><param name="to">The inclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateInclusiveBetweenAttribute(long from, long to, string message)
        : base(message)
    {
        this.From = SingleValueValidationCodeGenAttribute.Format(from);
        this.To = SingleValueValidationCodeGenAttribute.Format(to);
    }

    /// <summary>Initializes an inclusive range rule with floating-point bounds.</summary>
    /// <param name="from">The inclusive lower bound.</param><param name="to">The inclusive upper bound.</param>
    public ValidateInclusiveBetweenAttribute(double from, double to)
    {
        this.From = SingleValueValidationCodeGenAttribute.Format(from);
        this.To = SingleValueValidationCodeGenAttribute.Format(to);
    }

    /// <summary>Initializes an inclusive range rule with floating-point bounds and a custom message.</summary>
    /// <param name="from">The inclusive lower bound.</param><param name="to">The inclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateInclusiveBetweenAttribute(double from, double to, string message)
        : base(message)
    {
        this.From = SingleValueValidationCodeGenAttribute.Format(from);
        this.To = SingleValueValidationCodeGenAttribute.Format(to);
    }

    /// <summary>Initializes an inclusive range rule with invariant string bounds.</summary>
    /// <param name="from">The inclusive lower bound.</param><param name="to">The inclusive upper bound.</param>
    public ValidateInclusiveBetweenAttribute(string from, string to)
    {
        this.From = from;
        this.To = to;
    }

    /// <summary>Initializes an inclusive range rule with invariant string bounds and a custom message.</summary>
    /// <param name="from">The inclusive lower bound.</param><param name="to">The inclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateInclusiveBetweenAttribute(string from, string to, string message)
        : base(message)
    {
        this.From = from;
        this.To = to;
    }

    /// <summary>
    /// Gets the lower bound value.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Gets the upper bound value.
    /// </summary>
    public string To { get; }
}

/// <summary>
/// Marks a property to generate a FluentValidation <c>ExclusiveBetween</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateExclusiveBetweenAttribute : ValidationCodeGenAttribute
{
    /// <summary>Initializes an exclusive range rule with integer bounds.</summary>
    /// <param name="from">The exclusive lower bound.</param><param name="to">The exclusive upper bound.</param>
    public ValidateExclusiveBetweenAttribute(long from, long to)
    {
        this.From = SingleValueValidationCodeGenAttribute.Format(from);
        this.To = SingleValueValidationCodeGenAttribute.Format(to);
    }

    /// <summary>Initializes an exclusive range rule with integer bounds and a custom message.</summary>
    /// <param name="from">The exclusive lower bound.</param><param name="to">The exclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateExclusiveBetweenAttribute(long from, long to, string message)
        : base(message)
    {
        this.From = SingleValueValidationCodeGenAttribute.Format(from);
        this.To = SingleValueValidationCodeGenAttribute.Format(to);
    }

    /// <summary>Initializes an exclusive range rule with floating-point bounds.</summary>
    /// <param name="from">The exclusive lower bound.</param><param name="to">The exclusive upper bound.</param>
    public ValidateExclusiveBetweenAttribute(double from, double to)
    {
        this.From = SingleValueValidationCodeGenAttribute.Format(from);
        this.To = SingleValueValidationCodeGenAttribute.Format(to);
    }

    /// <summary>Initializes an exclusive range rule with floating-point bounds and a custom message.</summary>
    /// <param name="from">The exclusive lower bound.</param><param name="to">The exclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateExclusiveBetweenAttribute(double from, double to, string message)
        : base(message)
    {
        this.From = SingleValueValidationCodeGenAttribute.Format(from);
        this.To = SingleValueValidationCodeGenAttribute.Format(to);
    }

    /// <summary>Initializes an exclusive range rule with invariant string bounds.</summary>
    /// <param name="from">The exclusive lower bound.</param><param name="to">The exclusive upper bound.</param>
    public ValidateExclusiveBetweenAttribute(string from, string to)
    {
        this.From = from;
        this.To = to;
    }

    /// <summary>Initializes an exclusive range rule with invariant string bounds and a custom message.</summary>
    /// <param name="from">The exclusive lower bound.</param><param name="to">The exclusive upper bound.</param><param name="message">The custom validation message.</param>
    public ValidateExclusiveBetweenAttribute(string from, string to, string message)
        : base(message)
    {
        this.From = from;
        this.To = to;
    }

    /// <summary>
    /// Gets the lower bound value.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Gets the upper bound value.
    /// </summary>
    public string To { get; }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>MustNotBeEmptyGuid</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateNotEmptyGuidAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateNotEmptyGuidAttribute"/> class.
    /// </summary>
    public ValidateNotEmptyGuidAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateNotEmptyGuidAttribute"/> class.
    /// </summary>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateNotEmptyGuidAttribute(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>MustNotBeDefaultOrEmptyGuid</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateNotDefaultOrEmptyGuidAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateNotDefaultOrEmptyGuidAttribute"/> class.
    /// </summary>
    public ValidateNotDefaultOrEmptyGuidAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateNotDefaultOrEmptyGuidAttribute"/> class.
    /// </summary>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateNotDefaultOrEmptyGuidAttribute(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>MustBeValidGuid</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateValidGuidAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateValidGuidAttribute"/> class.
    /// </summary>
    public ValidateValidGuidAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateValidGuidAttribute"/> class.
    /// </summary>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateValidGuidAttribute(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>MustBeEmptyGuid</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateEmptyGuidAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateEmptyGuidAttribute"/> class.
    /// </summary>
    public ValidateEmptyGuidAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateEmptyGuidAttribute"/> class.
    /// </summary>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateEmptyGuidAttribute(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>MustBeDefaultOrEmptyGuid</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateDefaultOrEmptyGuidAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateDefaultOrEmptyGuidAttribute"/> class.
    /// </summary>
    public ValidateDefaultOrEmptyGuidAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateDefaultOrEmptyGuidAttribute"/> class.
    /// </summary>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateDefaultOrEmptyGuidAttribute(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>MustBeInGuidFormat</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateGuidFormatAttribute : ValidationCodeGenAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateGuidFormatAttribute"/> class.
    /// </summary>
    public ValidateGuidFormatAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateGuidFormatAttribute"/> class.
    /// </summary>
    /// <param name="message">The optional custom validation message.</param>
    public ValidateGuidFormatAttribute(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>EmailAddress</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateEmailAttribute : ValidationCodeGenAttribute
{
    /// <summary>Initializes an email-address validation rule.</summary>
    public ValidateEmailAttribute() { }

    /// <summary>Initializes an email-address validation rule with a custom message.</summary>
    /// <param name="message">The custom validation message.</param>
    public ValidateEmailAttribute(string message) : base(message) { }
}

/// <summary>
/// Marks a string property to generate a FluentValidation <c>Matches</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateMatchesAttribute : ValidationCodeGenAttribute
{
    /// <summary>Initializes a regular-expression validation rule.</summary>
    /// <param name="pattern">The regular expression that values must match.</param>
    public ValidateMatchesAttribute(string pattern) => this.Pattern = pattern;

    /// <summary>Initializes a regular-expression validation rule with a custom message.</summary>
    /// <param name="pattern">The regular expression that values must match.</param>
    /// <param name="message">The custom validation message.</param>
    public ValidateMatchesAttribute(string pattern, string message)
        : base(message) => this.Pattern = pattern;

    /// <summary>
    /// Gets the regex pattern.
    /// </summary>
    public string Pattern { get; }
}

/// <summary>
/// Marks a collection property to generate a FluentValidation <c>RuleForEach(...).NotNull()</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateEachNotNullAttribute : ValidationCodeGenAttribute
{
    /// <summary>Initializes a rule that requires each collection element to be non-null.</summary>
    public ValidateEachNotNullAttribute() { }

    /// <summary>Initializes a rule that requires each collection element to be non-null and uses a custom message.</summary>
    /// <param name="message">The custom validation message.</param>
    public ValidateEachNotNullAttribute(string message) : base(message) { }
}

/// <summary>
/// Marks a collection property to generate a FluentValidation <c>RuleForEach(...).NotEmpty()</c> rule.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ValidateEachNotEmptyAttribute : ValidationCodeGenAttribute
{
    /// <summary>Initializes a rule that requires each collection element to be non-empty.</summary>
    public ValidateEachNotEmptyAttribute() { }

    /// <summary>Initializes a rule that requires each collection element to be non-empty and uses a custom message.</summary>
    /// <param name="message">The custom validation message.</param>
    public ValidateEachNotEmptyAttribute(string message) : base(message) { }
}
