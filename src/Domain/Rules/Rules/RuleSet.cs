// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Text.RegularExpressions;
using FluentValidation;

/// <summary>
/// Provides a comprehensive set of validation rules for various types of values.
/// </summary>
public static class RuleSet
{
    /// <summary>
    /// Validates that two values are equal.
    /// Usage: ValueRules.Equal(status, Status.Active)
    /// </summary>
    public static IRule Equal<T>(T value, T other) =>
        new EqualRule<T>(value, other);

    /// <summary>
    /// Validates that two values are not equal.
    /// Usage: ValueRules.NotEqual(status, Status.Deleted)
    /// </summary>
    public static IRule NotEqual<T>(T value, T other) =>
        new NotEqualRule<T>(value, other);

    /// <summary>
    /// Validates that a value is greater than another value.
    /// Usage: ValueRules.GreaterThan(age, 18)
    /// </summary>
    public static IRule GreaterThan<T>(T value, T other)
        where T : IComparable<T> =>
        new GreaterThanRule<T>(value, other);

    /// <summary>
    /// Validates that a value is greater than or equal to another value.
    /// Usage: ValueRules.GreaterThanOrEqual(amount, 0)
    /// </summary>
    public static IRule GreaterThanOrEqual<T>(T value, T other)
        where T : IComparable<T> =>
        new GreaterThanOrEqualRule<T>(value, other);

    /// <summary>
    /// Validates that a value is less than another value.
    /// Usage: ValueRules.LessThan(temperature, 100)
    /// </summary>
    public static IRule LessThan<T>(T value, T other)
        where T : IComparable<T> =>
        new LessThanRule<T>(value, other);

    /// <summary>
    /// Validates that a value is less than or equal to another value.
    /// Usage: ValueRules.LessThanOrEqual(discount, 100)
    /// </summary>
    public static IRule LessThanOrEqual<T>(T value, T other)
        where T : IComparable<T> =>
        new LessThanOrEqualRule<T>(value, other);

    /// <summary>
    /// Validates that a value falls within a numeric range (inclusive).
    /// Usage: ValueRules.NumericRange(rating, 1, 5)
    /// </summary>
    public static IRule NumericRange<T>(T value, T min, T max)
        where T : IComparable<T> =>
        new NumericRangeRule<T>(value, min, max);

    /// <summary>
    /// Validates that a value is null.
    /// Usage: ValueRules.IsNull(optionalValue)
    /// </summary>
    public static IRule IsNull<T>(T value) =>
        new IsNullRule<T>(value);

    /// <summary>
    /// Validates that a value is not null.
    /// Usage: ValueRules.IsNotNull(requiredValue)
    /// </summary>
    public static IRule IsNotNull<T>(T value) =>
        new IsNotNullRule<T>(value);

    /// <summary>
    /// Validates that a collection or string is empty.
    /// Usage: ValueRules.IsEmpty(list)
    /// </summary>
    public static IRule IsEmpty<T>(T value) =>
        new IsEmptyRule<T>(value);

    /// <summary>
    /// Validates that a collection or string is not empty.
    /// Usage: ValueRules.IsNotEmpty(requiredList)
    /// </summary>
    public static IRule IsNotEmpty<T>(T value) =>
        new IsNotEmptyRule<T>(value);

    /// <summary>
    /// Validates that a string contains a substring.
    /// Usage: ValueRules.Contains(email, "@", StringComparison.OrdinalIgnoreCase)
    /// </summary>
    public static IRule Contains(
        string value,
        string substring,
        StringComparison comparison = StringComparison.Ordinal) =>
        new ContainsRule(value, substring, comparison);

    /// <summary>
    /// Validates that a string does not contain a substring.
    /// Usage: ValueRules.DoesNotContain(username, "admin", StringComparison.OrdinalIgnoreCase)
    /// </summary>
    public static IRule DoesNotContain(
        string value,
        string substring,
        StringComparison comparison = StringComparison.Ordinal) =>
        new DoesNotContainRule(value, substring, comparison);

    /// <summary>
    /// Validates that a string starts with a prefix.
    /// Usage: ValueRules.StartsWith(phoneNumber, "+1")
    /// </summary>
    public static IRule StartsWith(
        string value,
        string prefix,
        StringComparison comparison = StringComparison.Ordinal) =>
        new StartsWithRule(value, prefix, comparison);

    /// <summary>
    /// Validates that a string does not start with a prefix.
    /// Usage: ValueRules.DoesNotStartWith(email, "noreply")
    /// </summary>
    public static IRule DoesNotStartWith(
        string value,
        string prefix,
        StringComparison comparison = StringComparison.Ordinal) =>
        new DoesNotStartWithRule(value, prefix, comparison);

    /// <summary>
    /// Validates that a string ends with a suffix.
    /// Usage: ValueRules.EndsWith(email, "@company.com")
    /// </summary>
    public static IRule EndsWith(
        string value,
        string suffix,
        StringComparison comparison = StringComparison.Ordinal) =>
        new EndsWithRule(value, suffix, comparison);

    /// <summary>
    /// Validates that a string does not end with a suffix.
    /// Usage: ValueRules.DoesNotEndWith(domain, ".temp")
    /// </summary>
    public static IRule DoesNotEndWith(
        string value,
        string suffix,
        StringComparison comparison = StringComparison.Ordinal) =>
        new DoesNotEndWithRule(value, suffix, comparison);

    /// <summary>
    /// Validates that a date falls within a specific range.
    /// Usage: ValueRules.DateRange(appointmentDate, startDate, endDate, inclusive: true)
    /// </summary>
    public static IRule IsInRange(DateTime value, DateTime start, DateTime end, bool inclusive = true) =>
        new DateRangeRule(value, start, end, inclusive);

    /// <summary>
    /// Validates that a date falls within a relative range (e.g., next 7 days).
    /// Usage: ValueRules.DateRelative(dueDate, DateRelativeUnit.Day, 7, RelativeDirection.Future)
    /// </summary>
    public static IRule IsInRelativeRange(
        DateTime value,
        DateUnit unit,
        int amount,
        DateTimeDirection direction) =>
        new DateRelativeRule(value, unit, amount, direction);

    /// <summary>
    /// Validates that a time falls within a specific range.
    /// Usage: ValueRules.TimeRange(openingTime, TimeOnly.Parse("09:00"), TimeOnly.Parse("17:00"))
    /// </summary>
    public static IRule IsInRange(TimeOnly value, TimeOnly start, TimeOnly end, bool inclusive = true) =>
        new TimeRangeRule(value, start, end, inclusive);

    /// <summary>
    /// Validates that a time falls within a relative range (e.g., next 2 hours).
    /// Usage: ValueRules.TimeRelative(meetingTime, TimeRelativeUnit.Hour, 2, RelativeDirection.Future)
    /// </summary>
    public static IRule IsInRelativeRange(
        TimeOnly value,
        TimeUnit unit,
        int amount,
        DateTimeDirection direction) =>
        new TimeRelativeRule(value, unit, amount, direction);

    /// <summary>
    /// Validates that any element in a collection satisfies a rule.
    /// Usage: ValueRules.Any(users, user => ValueRules.GreaterThan(user.Age, 18))
    /// </summary>
    public static IRule Any<T>(IEnumerable<T> collection, Func<T, IRule> ruleFactory) =>
        new AnyRule<T>(collection, ruleFactory);

    /// <summary>
    /// Validates that all elements in a collection satisfy a rule.
    /// Usage: ValueRules.All(orderItems, item => ValueRules.GreaterThan(item.Quantity, 0))
    /// </summary>
    public static IRule All<T>(IEnumerable<T> collection, Func<T, IRule> ruleFactory) =>
        new AllRule<T>(collection, ruleFactory);

    /// <summary>
    /// Validates that no element in a collection satisfies a rule.
    /// Usage: ValueRules.None(products, product => ValueRules.Equal(product.Status, Status.Discontinued))
    /// </summary>
    public static IRule None<T>(IEnumerable<T> collection, Func<T, IRule> ruleFactory) =>
        new NoneRule<T>(collection, ruleFactory);

    /// <summary>
    /// Validates that a value exists in a collection.
    /// Usage: ValueRules.In(status, new[] { Status.Active, Status.Pending })
    /// </summary>
    public static IRule In<T>(T value, IEnumerable<T> allowedValues) =>
        new InRule<T>(value, allowedValues);

    /// <summary>
    /// Validates that a value does not exist in a collection.
    /// Usage: ValueRules.NotIn(username, reservedUsernames)
    /// </summary>
    public static IRule NotIn<T>(T value, IEnumerable<T> disallowedValues) =>
        new NotInRule<T>(value, disallowedValues);

    /// <summary>
    /// Validates text against a full-text search query.
    /// Usage: ValueRules.FullTextSearch(document.Content, "important meeting", searchFields)
    /// </summary>
    public static IRule FullTextSearch(
        string text,
        string searchTerm,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase) =>
        new FullTextSearchRule(text, searchTerm, comparison);

    /// <summary>
    /// Validates that a string is a valid email address.
    /// Usage: ValueRules.IsEmail("test@example.com")
    /// </summary>
    public static IRule IsValidEmail(string value) =>
        new ValidEmailRule(value);

    /// <summary>
    /// Validates that an enum value is one of the allowed values.
    /// Usage: ValueRules.EnumValues(status, new[] { Status.Active, Status.Pending })
    /// </summary>
    public static IRule HasValues<TEnum>(TEnum value, IEnumerable<TEnum> allowedValues)
        where TEnum : struct, Enum =>
        new EnumValuesRule<TEnum>(value, allowedValues);

    /// <summary>
    /// Validates that a value matches a regular expression pattern.
    /// Usage: ValueRules.Pattern(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")
    /// </summary>
    public static IRule MatchesPattern(string value, string pattern) =>
        new PatternRule(value, pattern);

    /// <summary>
    /// Validates that a string has a specific length range.
    /// Usage: ValueRules.StringLength(password, 8, 20)
    /// </summary>
    public static IRule HasStringLength(string value, int minLength, int maxLength) =>
        new StringLengthRule(value, minLength, maxLength);

    /// <summary>
    /// Validates that a collection has a specific size range.
    /// Usage: ValueRules.CollectionSize(items, 1, 10)
    /// </summary>
    public static IRule HasCollectionSize<T>(IEnumerable<T> collection, int minSize, int maxSize) =>
        new CollectionSizeRule<T>(collection, minSize, maxSize);

    /// <summary>
    /// Validates text against a list of allowed words.
    /// Usage: ValueRules.TextIn(category, new[] { "Electronics", "Books", "Clothing" })
    /// </summary>
    public static IRule TextIn(
        string value,
        IEnumerable<string> allowedValues,
        StringComparison comparison = StringComparison.Ordinal) =>
        new TextInRule(value, allowedValues, comparison);

    /// <summary>
    /// Validates text against a list of disallowed words.
    /// Usage: ValueRules.TextNotIn(username, reservedUsernames)
    /// </summary>
    public static IRule TextNotIn(
        string value,
        IEnumerable<string> disallowedValues,
        StringComparison comparison = StringComparison.Ordinal) =>
        new TextNotInRule(value, disallowedValues, comparison);

    /// <summary>
    /// Validates that a numeric value is in a list of allowed values.
    /// Usage: ValueRules.NumericIn(quantity, new[] { 1, 5, 10 })
    /// </summary>
    public static IRule NumericIn<T>(T value, IEnumerable<T> allowedValues)
        where T : IComparable<T> =>
        new NumericInRule<T>(value, allowedValues);

    /// <summary>
    /// Validates that a numeric value is not in a list of disallowed values.
    /// Usage: ValueRules.NumericNotIn(quantity, new[] { 0, -1 })
    /// </summary>
    public static IRule NumericNotIn<T>(T value, IEnumerable<T> disallowedValues)
        where T : IComparable<T> =>
        new NumericNotInRule<T>(value, disallowedValues);

    /// <summary>
    /// Validates an object using a FluentValidation validator.
    /// Usage: ValueRules.ValidateWith(customer, new CustomerValidator())
    /// </summary>
    public static IRule Validate<T>(T instance, IValidator<T> validator) =>
        new ValidationRule<T>(instance, validator);
}

/// <summary>
/// Represents a validation rule that checks if two values are equal.
/// </summary>
/// <typeparam name="T">The type of the values being compared.</typeparam>
public class EqualRule<T>(T value, T other) : RuleBase
{
    /// <summary>
    /// Gets the message that describes the outcome of the rule evaluation.
    /// </summary>
    /// <remarks>
    /// In the context of the derived class <see cref="EqualRule{T}"/>, the message indicates that
    /// the value must be equal to the specified other value.
    /// </remarks>
    public override string Message => $"Value must be equal to {other}";

    /// <summary>
    /// Executes the rule logic associated with this rule.
    /// </summary>
    /// <returns>A Result object indicating the outcome of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(EqualityComparer<T>.Default.Equals(value, other));
}

/// <summary>
/// Represents a validation rule that checks if two values are not equal.
/// </summary>
/// <typeparam name="T">The type of the values being compared.</typeparam>
public class NotEqualRule<T>(T value, T other) : RuleBase
{
    /// <summary>
    /// Represents a simple message with a sender and content.
    /// </summary>
    public override string Message => $"Value must not be equal to {other}";

    /// <summary>
    /// Executes the validation rule, returning a success result if the rule passes and a failure result if it does not.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the rule.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(!EqualityComparer<T>.Default.Equals(value, other));
}

/// <summary>
/// Represents a validation rule that checks if a value is greater than a specified value.
/// </summary>
/// <typeparam name="T">The type of the values being compared, which must implement IComparable{T}.</typeparam>
public class GreaterThanRule<T>(T value, T other)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets the message associated with the rule. This message provides information about the rule failure reason.
    /// </summary>
    public override string Message => $"Value must be greater than {other}";

    /// <summary>
    /// Executes a validation rule and returns the result.
    /// </summary>
    /// <returns>
    /// Result indicating the success or failure of the rule execution.
    /// </returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value.CompareTo(other) > 0);
}

/// <summary>
/// Represents a validation rule that checks if a value is greater than or equal to a specified value.
/// Uses the <see cref="IComparable{T}"/> interface for comparison.
/// </summary>
/// <typeparam name="T">The type of the values being compared.</typeparam>
public class GreaterThanOrEqualRule<T>(T value, T other)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets the message associated with the rule.
    /// </summary>
    public override string Message => $"Value must be greater than or equal to {other}";

    /// <summary>
    /// Executes the rule and returns the result of the execution.
    /// </summary>
    /// <returns>
    /// A result indicating the success or failure of the rule execution.
    /// </returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value.CompareTo(other) >= 0);
}

/// <summary>
/// Represents a validation rule that checks if a value is less than a specified value.
/// </summary>
/// <typeparam name="T">The type of the values being compared, which must implement IComparable&lt;T&gt;.</typeparam>
public class LessThanRule<T>(T value, T other)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets a message describing the outcome of the rule execution.
    /// </summary>
    public override string Message => $"Value must be less than {other}";

    /// <summary>
    /// Executes the rule and returns a success result if validation passes.
    /// </summary>
    /// <returns>A Result indicating the success or failure of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value.CompareTo(other) < 0);
}

/// <summary>
/// Represents a validation rule that checks whether a given value is less than or equal to a specified value.
/// </summary>
/// <typeparam name="T">The type of the values being compared. Must implement IComparable<T>.</typeparam>
public class LessThanOrEqualRule<T>(T value, T other)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets the message associated with the rule.
    /// </summary>
    public override string Message => $"Value must be less than or equal to {other}";

    /// <summary>
    /// Executes the defined rule and returns the result of the execution.
    /// </summary>
    /// <returns>The outcome of the rule execution as a Result object.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value.CompareTo(other) <= 0);
}

/// <summary>
/// Represents a validation rule that checks if a numeric value falls within a specified range.
/// </summary>
/// <typeparam name="T">The type of the numeric value, which must implement IComparable&lt;T&gt;.</typeparam>
public class NumericRangeRule<T>(T value, T min, T max)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Message providing additional information about the rule's execution result.
    /// Derived classes can override this property to provide a more specific message context.
    /// </summary>
    public override string Message => $"Value must be between {min} and {max}";

    /// <summary>
    /// Executes the specific rule defined in the derived class.
    /// </summary>
    /// <returns>A <c>Result</c> object representing the success or failure of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0);
}

/// <summary>
/// Rule for validating if a given value is null.
/// </summary>
public class IsNullRule<T>(T value) : RuleBase
{
    /// <summary>
    /// Gets the message associated with the rule. Provides a human-readable explanation
    /// of why the rule failed or what condition the rule checks for.
    /// </summary>
    public override string Message => "Value must be null";

    /// <summary>
    /// Executes a validation rule.
    /// </summary>
    /// <returns>A Result indicating the outcome of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value is null);
}

/// <summary>
/// Defines a validation rule that checks whether a given value is not null.
/// The rule returns a success result if the value is not null; otherwise, it returns a failure result.
/// </summary>
/// <typeparam name="T">The type of the value to be validated.</typeparam>
public class IsNotNullRule<T>(T value) : RuleBase
{
    /// <summary>
    /// Sends a message to the console and logs the message.
    /// </summary>
    /// <param name="message">The message to be sent and logged.</param>
    public override string Message => "Value must not be null";

    /// <summary>
    /// Executes a business rule encapsulated within an Action delegate.
    /// Ensures that the provided rule is invoked with the given context.
    /// </summary>
    /// <param name="rule">The business rule to be executed as an Action delegate.</param>
    /// <param name="context">The context or parameters required by the rule to operate.</param>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value is not null);
}

/// <summary>
/// Represents a validation rule that checks if a given value is empty.
/// </summary>
public class IsEmptyRule<T>(T value) : RuleBase
{
    /// <summary>
    /// Gets the message that will be displayed when the rule fails.
    /// </summary>
    public override string Message => "Value must be empty";

    /// <summary>
    /// Executes the rule associated with the current instance.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value switch
        {
            string s => string.IsNullOrEmpty(s),
            IEnumerable<object> e => !e.Any(),
            Guid g => g == Guid.Empty,
            null => true,
            _ => false
        });
}

/// <summary>
/// Validates that a value is not empty according to specific rules for different types.
/// </summary>
/// <typeparam name="T">The type of the value being validated.</typeparam>
public class IsNotEmptyRule<T>(T value) : RuleBase
{
    /// <summary>
    /// Gets the message describing the outcome of the rule execution.
    /// </summary>
    public override string Message => "Value must not be empty";

    /// <summary>
    /// Executes a rule, returning a result indicating success or failure.
    /// </summary>
    /// <returns>A Result object representing the outcome of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value switch
        {
            string s => !string.IsNullOrEmpty(s),
            IEnumerable<object> e => e.Any(),
            Guid g => g != Guid.Empty,
            null => false,
            _ => true
        });
}

/// <summary>
/// Validates that a specified string contains a given substring with a specified string comparison option.
/// </summary>
/// <remarks>
/// This rule checks whether the main string contains the specified substring using the comparison rules provided.
/// If checked string is empty or null, the validation fails.
/// </remarks>
public class ContainsRule(string value, string substring, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets the message that describes the result of applying the rule.
    /// </summary>
    public override string Message => $"Value must contain '{substring}'";

    /// <summary>
    /// Executes the specified rule.
    /// </summary>
    /// <returns>The result of the executed rule.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(!string.IsNullOrEmpty(value) &&
            value.Contains(substring, comparison));
}

/// <summary>
/// Represents a rule that validates whether a string does not contain a specified substring.
/// </summary>
public class DoesNotContainRule(string value, string substring, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Provides a descriptive message that explains why the rule failed.
    /// This message is intended to be user-friendly and specific to the particular rule implementation.
    /// </summary>
    public override string Message => $"Value must not contain '{substring}'";

    /// <summary>
    /// Executes a predefined validation rule.
    /// </summary>
    /// <returns>
    /// A Result object indicating the success or failure of the rule execution.
    /// </returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(string.IsNullOrEmpty(value) ||
            !value.Contains(substring, comparison));
}

/// <summary>
/// Validates whether a specified string value starts with a given prefix using the specified string comparison option.
/// </summary>
public class StartsWithRule(string value, string prefix, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets the message associated with the rule, providing detailed information about the rule's constraint.
    /// </summary>
    public override string Message => $"Value must start with '{prefix}'";

    /// <summary>
    /// Executes the rule and returns the result.
    /// </summary>
    /// <returns>Success if the rule is satisfied; otherwise, an error result.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(!string.IsNullOrEmpty(value) &&
            value.StartsWith(prefix, comparison));
}

/// <summary>
/// Represents a validation rule that checks if a value does not start with a specified prefix.
/// </summary>
public class DoesNotStartWithRule(string value, string prefix, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets a descriptive message associated with the rule, which typically describes why the rule failed.
    /// </summary>
    public override string Message => $"Value must not start with '{prefix}'";

    /// <summary>
    /// Executes a specified business rule and returns the result of the execution.
    /// </summary>
    /// <param name="rule">The business rule to execute.</param>
    /// <param name="context">The context in which the rule is executed.</param>
    /// <return>Returns the result of the rule execution.</return>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(string.IsNullOrEmpty(value) ||
            !value.StartsWith(prefix, comparison));
}

/// <summary>
/// Represents a validation rule that checks if a given string value ends with a specified suffix.
/// </summary>
public class EndsWithRule(string value, string suffix, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets the message associated with the rule, indicating the specific requirement or error condition.
    /// </summary>
    public override string Message => $"Value must end with '{suffix}'";

    /// <summary>
    /// Executes a rule and returns a result indicating success or failure.
    /// </summary>
    /// <returns>A Result object indicating the outcome of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(!string.IsNullOrEmpty(value) &&
            value.EndsWith(suffix, comparison));
}

/// <summary>
/// Represents a validation rule that checks whether a given string does not end with a specified suffix.
/// </summary>
public class DoesNotEndWithRule(string value, string suffix, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets the message associated with the validation rule.
    /// </summary>
    /// <remarks>
    /// The message provides a human-readable description of why the rule was not satisfied.
    /// Each specific rule implementation can override this property to provide a more detailed message.
    /// </remarks>
    public override string Message => $"Value must not end with '{suffix}'";

    /// <summary>
    /// Executes the rule logic and returns a result indicating the success or failure of the rule evaluation.
    /// </summary>
    /// <returns>
    /// A result object representing the outcome of the rule execution.
    /// </returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(string.IsNullOrEmpty(value) ||
            !value.EndsWith(suffix, comparison));
}

/// <summary>
/// Represents a validation rule that checks if a specified date is within a given range.
/// </summary>
/// <param name="value">The date value to be validated.</param>
/// <param name="start">The start date of the range.</param>
/// <param name="end">The end date of the range.</param>
/// <param name="inclusive">Indicates whether the range is inclusive of the start and end dates.</param>
public class DateRangeRule(DateTime value, DateTime start, DateTime end, bool inclusive) : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// The message content.
    /// </value>
    public override string Message => $"Date must be {(inclusive ? "between" : "strictly between")} {start} and {end}";

    /// <summary>
    /// Executes a specified rule logic.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="context">The context in which the rule is executed.</param>
    /// <returns>
    /// A boolean indicating whether the rule execution was successful.
    /// </returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value.IsInRange(start, end, inclusive));
}

/// <summary>
/// Defines a rule that evaluates whether a given time is within a specified range.
/// </summary>
/// <param name="value">The time value to evaluate.</param>
/// <param name="start">The start of the time range.</param>
/// <param name="end">The end of the time range.</param>
/// <param name="inclusive">Indicates whether the range is inclusive of the start and end times.</param>
public class TimeRangeRule(TimeOnly value, TimeOnly start, TimeOnly end, bool inclusive) : RuleBase
{
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <remarks>
    /// This property holds the textual content that can be used for displaying
    /// messages, notifications, or communications within the application.
    /// The content of this string can vary depending on the context in which it's used.
    /// </remarks>
    public override string Message => $"Time must be {(inclusive ? "between" : "strictly between")} {start} and {end}";

    /// <summary>
    /// Executes a validation rule and returns the result.
    /// </summary>
    /// <returns>
    /// A result object indicating the success or failure of the rule execution.
    /// </returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value.IsInRange(start, end, inclusive));
}

/// <summary>
/// Represents a validation rule that checks if a given date is within a specified range relative to the current date.
/// </summary>
public class DateRelativeRule(DateTime value, DateUnit unit, int amount, DateTimeDirection direction) : RuleBase
{
    /// <summary>
    /// Gets the message describing the result of the rule evaluation.
    /// </summary>
    /// <remarks>
    /// This message provides information about why the rule was not satisfied.
    /// The default implementation returns "Rule not satisfied". Derived classes
    /// can override this property to provide specific messages relevant to the rule.
    /// </remarks>
    public override string Message => $"Date must be within {amount} {unit}(s) {direction.ToString().ToLower()} from now";

    /// <summary>
    /// Executes the rule, determining if it passes or fails.
    /// </summary>
    /// <returns>A result indicating the success or failure of the rule.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value.IsInRelativeRange(unit, amount, direction));
}

/// <summary>
/// Represents a rule that validates whether a specified time value is within a given relative range from the current time.
/// </summary>
/// <param name="value">The time value to be validated.</param>
/// <param name="unit">The time unit (Minute, Hour) used to define the range.</param>
/// <param name="amount">The amount of the time unit to define the range.</param>
/// <param name="direction">The direction (Past, Future) from the current time for the validation.</param>
public class TimeRelativeRule(TimeOnly value, TimeUnit unit, int amount, DateTimeDirection direction) : RuleBase
{
    /// <summary>
    /// Gets the descriptive message associated with the rule, indicating the condition that must be met.
    /// </summary>
    public override string Message => $"Time must be within {amount} {unit}(s) {direction.ToString().ToLower()} from now";

    /// <summary>
    /// Executes the validation rule and returns a Result object indicating the success or failure of the rule.
    /// </summary>
    /// <returns>
    /// A Result object containing the outcome of the rule execution.
    /// </returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value.IsInRelativeRange(unit, amount, direction));
}

/// <summary>
/// Represents a rule that validates if any element in a collection satisfies a specified condition.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
/// <param name="collection">The collection of elements to be validated.</param>
/// <param name="ruleFactory">A function that generates a rule for each element in the collection.</param>
public class AnyRule<T>(IEnumerable<T> collection, Func<T, IRule> ruleFactory)
    : RuleBase
{
    /// <summary>
    /// Provides a description or reason why the rule was not satisfied.
    /// </summary>
    public override string Message => "No element in the collection satisfies the condition";

    /// <summary>
    /// Executes a specified rule and returns the result of the execution.
    /// </summary>
    /// <typeparam name="TRule">The type of the rule to execute.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the rule.</typeparam>
    /// <param name="rule">The rule instance to execute.</param>
    /// <returns>The result of executing the rule.</returns>
    protected override Result ExecuteRule()
    {
        if (collection?.Any() != true)
        {
            return Result.Failure();
        }

        return Result.SuccessIf(collection.Any(item =>
            ruleFactory(item).Apply().IsSuccess));
    }
}

/// <summary>
/// Represents a collection of all the rules to be applied for a specific operation or validation process.
/// </summary>
public class AllRule<T>(IEnumerable<T> collection, Func<T, IRule> ruleFactory)
    : RuleBase
{
    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    /// <value>
    /// The text content of the message.
    /// </value>
    public override string Message =>
        "Not all elements in the collection satisfy the condition";

    /// <summary>
    /// Executes the defined rule and returns a <see cref="Result"/> indicating success or failure.
    /// </summary>
    /// <returns>A <see cref="Result"/> object representing the outcome of the rule execution.</returns>
    protected override Result ExecuteRule()
    {
        if (collection?.Any() != true)
        {
            return Result.Failure();
        }

        return Result.SuccessIf(collection.All(item =>
            ruleFactory(item).Apply().IsSuccess));
    }
}

/// <summary>
/// Implements a validation rule that always returns true, indicating no validation checks are performed.
/// </summary>
public class NoneRule<T>(IEnumerable<T> collection, Func<T, IRule> ruleFactory)
    : RuleBase
{
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>
    /// A string representing the content of the message.
    /// </value>
    public override string Message =>
        "Some elements in the collection satisfy the condition when none should";

    /// <summary>
    /// Executes a specified rule.
    /// </summary>
    /// <param name="ruleName">The name of the rule to execute.</param>
    /// <param name="context">The context in which the rule is to be executed.</param>
    /// <returns>The result of the rule execution.</returns>
    protected override Result ExecuteRule()
    {
        if (collection?.Any() != true)
        {
            return Result.Success();
        }

        return Result.SuccessIf(collection.All(item =>
            !ruleFactory(item).Apply().IsSuccess));
    }
}

/// <summary>
/// Represents a single validation rule that can be applied to input values.
/// </summary>
public class InRule<T>(T value, IEnumerable<T> allowedValues)
    : RuleBase
{
    /// <summary>
    /// Gets or sets the message text.
    /// </summary>
    /// <value>
    /// The message text content.
    /// </value>
    public override string Message => $"Value must be one of: {string.Join(", ", allowedValues)}";

    /// <summary>
    /// Executes the specified rule with provided parameters.
    /// </summary>
    /// <param name="ruleName">The name of the rule to execute.</param>
    /// <param name="parameters">An array of objects representing the parameters for the rule.</param>
    /// <return>Returns the result of rule execution.</return>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(allowedValues.Contains(value));
}

/// <summary>
/// Specifies a validation rule that checks whether a given value is not within a specified set of disallowed values.
/// </summary>
public class NotInRule<T>(T value, IEnumerable<T> disallowedValues)
    : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public override string Message => $"Value must not be one of: {string.Join(", ", disallowedValues)}";

    /// <summary>
    /// Executes a specified rule and returns the result.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <return>Returns true if the rule executes successfully; otherwise, false.</return>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(!disallowedValues.Contains(value));
}

/// <summary>
/// Defines a rule to support full-text search functionality.
/// This class encapsulates the criteria and behavior required
/// to perform a full-text search in a given context.
/// </summary>
public class FullTextSearchRule(
    string text,
    string searchTerms,
    StringComparison comparison = StringComparison.OrdinalIgnoreCase) : RuleBase
{
    /// <summary>
    /// Gets or sets the message associated with the object.
    /// </summary>
    /// <value>
    /// A string representing the message.
    /// </value>
    public override string Message => "Text does not match search criteria";

    /// <summary>
    /// Executes a specific rule provided as a parameter.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="context">The context in which the rule is executed.</param>
    /// <returns>True if the rule is executed successfully, otherwise false.</returns>
    protected override Result ExecuteRule()
    {
        if (string.IsNullOrWhiteSpace(searchTerms))
        {
            return Result.Success();
        }

        var sarchTerm = searchTerms.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var searchText = text ?? string.Empty;

        return Result.SuccessIf(sarchTerm.All(term => searchText.Contains(term, comparison)));
    }
}

/// <summary>
/// Represents a validation rule for ensuring that a value adheres to a predefined set of enumerated values.
/// </summary>
public class EnumValuesRule<TEnum>(TEnum value, IEnumerable<TEnum> allowedValues)
    : RuleBase
    where TEnum : struct, Enum
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// A string representing the body of the message.
    /// </value>
    public override string Message =>
        $"Value must be one of: {string.Join(", ", allowedValues)}";

    /// <summary>
    /// Executes the specified rule.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="context">The context in which the rule is executed.</param>
    /// <returns>The result of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(allowedValues.Contains(value));
}

/// <summary>
/// Represents a rule that validates strings based on specific patterns.
/// </summary>
public class PatternRule(string value, string pattern) : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// The message content as a string.
    /// </value>
    public override string Message =>
        $"Value does not match pattern: {pattern}";

    /// <summary>
    /// Executes a specified rule based on the given parameters.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="parameters">Parameters required for the rule execution.</param>
    /// <returns>The result of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(!string.IsNullOrEmpty(value) &&
            System.Text.RegularExpressions.Regex.IsMatch(value, pattern));
}

/// <summary>
/// Enforces constraints on the length of string values ensuring they meet specified minimum and maximum length criteria.
/// </summary>
public class StringLengthRule(string value, int minLength, int maxLength) : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// The message is represented as a string, and it contains the content that will be transmitted or displayed.
    /// </value>
    public override string Message =>
        $"Text length must be between {minLength} and {maxLength} characters";

    /// <summary>
    /// Executes the provided rule and returns a boolean indicating success or failure.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="context">The context in which the rule should be executed.</param>
    /// <return>A boolean indicating whether the rule executed successfully or not.</return>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(value?.Length >= minLength && value?.Length <= maxLength);
}

/// <summary>
/// Enforces constraints on the size of a collection, ensuring it meets specified criteria.
/// </summary>
public class CollectionSizeRule<T>(IEnumerable<T> collection, int minSize, int maxSize) : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    /// <value>
    /// A string representing the message content.
    /// </value>
    public override string Message =>
        $"Collection size must be between {minSize} and {maxSize} items";

    /// <summary>
    /// Executes a specified rule and returns the result.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <param name="context">The context in which the rule should be executed.</param>
    /// <returns>The result of the executed rule.</returns>
    protected override Result ExecuteRule()
    {
        var count = collection?.Count() ?? 0;

        return Result.SuccessIf(count >= minSize && count <= maxSize);
    }
}

/// <summary>
/// Represents a rule for validating whether a specific text input adheres to predefined criteria.
/// </
public class TextInRule(
    string value,
    IEnumerable<string> allowedValues,
    StringComparison comparison) : RuleBase
{
    /// <summary>
    /// Gets a message that describes the result of the rule.
    /// </summary>
    /// <remarks>
    /// This property is typically used to provide a user-friendly explanation or reason for why a rule passed or failed.
    /// Each specific rule implementation may override this property to provide a more detailed or contextual message.
    /// </remarks>
    public override string Message =>
        $"Text must be one of: {string.Join(", ", allowedValues)}";

    /// <summary>
    /// Executes the rule logic and returns a Result indicating the success or failure of the rule.
    /// </summary>
    /// <returns>
    /// A Result indicating whether the rule was successfully executed.
    /// </returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(allowedValues.Any(allowed =>
            string.Equals(value, allowed, comparison)));
}

/// <summary>
/// Represents a validation rule that checks if a given text is not part of a specified list of disallowed values.
/// </summary>
/// <param name="value">The text value to be checked against the disallowed values.</param>
/// <param name="disallowedValues">A collection of disallowed text values that the <paramref name="value"/> should not match.</param>
/// <param name="comparison">Specifies the string comparison option to use when comparing the text value and the disallowed values.</param>
public class TextNotInRule(
    string value,
    IEnumerable<string> disallowedValues,
    StringComparison comparison) : RuleBase
{
    /// <summary>
    /// Gets the message that describes the result of the rule evaluation.
    /// </summary>
    /// <remarks>
    /// The <c>Message</c> property provides a locally overridden, generally user-friendly message
    /// that describes why a rule was not satisfied. If no local override is provided,
    /// a default message "Rule not satisfied" from the base class will be used.
    /// </remarks>
    public override string Message =>
        $"Text must not be one of: {string.Join(", ", disallowedValues)}";

    /// <summary>
    /// Executes the rule and returns the result.
    /// </summary>
    /// <returns>
    /// The result of the rule execution.
    /// </returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(!disallowedValues.Any(disallowed =>
            string.Equals(value, disallowed, comparison)));
}

/// <summary>
/// Represents a validation rule that checks if a numeric value is within a set of allowed values.
/// </summary>
/// <typeparam name="T">The type of the value being validated. Must implement IComparable&lt;T&gt;.</typeparam>
public class NumericInRule<T>(T value, IEnumerable<T> allowedValues)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Represents the validation message returned by a rule when it is not satisfied.
    /// </summary>
    public override string Message =>
        $"Value must be one of: {string.Join(", ", allowedValues)}";

    /// <summary>
    /// Executes the rule to produce a result.
    /// </summary>
    /// <returns>The result of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(allowedValues.Any(allowed =>
            value.CompareTo(allowed) == 0));
}

/// <summary>
/// Represents a validation rule that checks if a given numeric value is not
/// contained within a specified set of disallowed values.
/// </summary>
/// <typeparam name="T">The type of the value being validated, which must implement IComparable<T
public class NumericNotInRule<T>(T value, IEnumerable<T> disallowedValues)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets the message associated with the rule indicating the reason why the rule was not satisfied.
    /// </summary>
    /// <remarks>
    /// This property provides the specific message that describes the constraint or validation error.
    /// Override this property in derived classes to provide a meaningful message for the rule.
    /// </remarks>
    public override string Message =>
        $"Value must not be one of: {string.Join(", ", disallowedValues)}";

    /// <summary>
    /// Executes a validation rule.
    /// </summary>
    /// <returns>Result indicating success or failure of the rule execution.</returns>
    protected override Result ExecuteRule() =>
        Result.SuccessIf(disallowedValues.All(disallowed => value.CompareTo(disallowed) != 0));
}

/// <summary>
/// Validates whether a given string is a valid email address format.
/// </summary>
public partial class ValidEmailRule(string value) : RuleBase
{
    // Using source generation for regex compilation
    /// <summary>
    /// Represents a regular expression pattern used to validate email addresses.
    /// </summary>
    /// <returns>True if the input string is a valid email address; otherwise, false.</returns>
    [GeneratedRegex(
        @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
        @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 3000)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Gets or sets the message associated with this instance.
    /// </summary>
    /// <value>
    /// The message providing details or information.
    /// </value>
    public override string Message => "Invalid email address";

    /// <summary>
    /// Executes a given business rule, performing its associated action.
    /// </summary>
    /// <param name="rule">The business rule to be executed.</param>
    /// <param name="context">The context in which the rule should be executed.</param>
    /// <returns>Returns true if the rule execution is successful; otherwise, false.</returns>
    protected override Result ExecuteRule()
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure();
        }

        return Result.SuccessIf(EmailRegex().IsMatch(value));
    }
}

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
    protected override Result ExecuteRule()
    {
        var validationResult = validator.Validate(instance);

        return validationResult.IsValid
            ? Result.Success()
            : Result.Failure().WithError(new FluentValidationError(this, validationResult));
    }
}