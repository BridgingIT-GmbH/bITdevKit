// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

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
    public static IRule IsEmpty(string value) =>
        new IsEmptyRule(value);

    /// <summary>
    /// Validates that a collection or string is not empty.
    /// Usage: ValueRules.IsNotEmpty(requiredList)
    /// </summary>
    public static IRule IsNotEmpty(string value) =>
        new IsNotEmptyRule(value);

    /// <summary>
    /// Validates that a collection or string is empty.
    /// Usage: ValueRules.IsEmpty(list)
    /// </summary>
    public static IRule IsEmpty<T>(IEnumerable<T> value) =>
        new IsEmptyRule<T>(value);

    /// <summary>
    /// Validates that a collection or string is not empty.
    /// Usage: ValueRules.IsNotEmpty(requiredList)
    /// </summary>
    public static IRule IsNotEmpty<T>(IEnumerable<T> value) =>
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

    // Creates a rule to check if a DateTime is before another specified date
    public static IRule IsBefore(DateTime value, DateTime comparisonDate) =>
        new IsBeforeRule(value, comparisonDate);

    // Creates a rule to check if a DateTime is after another specified date
    public static IRule IsAfter(DateTime value, DateTime comparisonDate) =>
        new IsAfterRule(value, comparisonDate);

    // Creates a rule to check if a boolean value is true
    public static IRule IsTrue(bool value) =>
        new IsTrueRule(value);

    // Creates a rule to check if a boolean value is false
    public static IRule IsFalse(bool value) =>
        new IsFalseRule(value);

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