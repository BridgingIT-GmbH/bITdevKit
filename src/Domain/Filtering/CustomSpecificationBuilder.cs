// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Globalization;

public static class CustomSpecificationBuilder
{
    public static ISpecification<TEntity> Build<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (filter == null)
        {
            throw new ArgumentException("Filter criteria is required.");
        }

        return filter.CustomType switch
        {
            FilterCustomType.IsNull => BuildIsNull<TEntity>(filter),
            FilterCustomType.IsNotNull => BuildIsNotNull<TEntity>(filter),
            FilterCustomType.FullTextSearch => BuildFullTextSearch<TEntity>(filter),
            FilterCustomType.DateRange => BuildDateRange<TEntity>(filter),
            FilterCustomType.DateRelative => BuildDateRelative<TEntity>(filter),
            FilterCustomType.TimeRange => BuildTimeRange<TEntity>(filter),
            FilterCustomType.NumericRange => BuildNumericRange<TEntity>(filter),
            FilterCustomType.EnumValues => BuildEnumValues<TEntity>(filter),
            FilterCustomType.TextIn => BuildTextIn<TEntity>(filter),
            FilterCustomType.TextNotIn => BuildTextNotIn<TEntity>(filter),
            FilterCustomType.NumericIn => BuildNumericIn<TEntity>(filter),
            FilterCustomType.NumericNotIn => BuildNumericNotIn<TEntity>(filter),
            _ => throw new NotSupportedException($"Custom filter type {filter.CustomType} is not supported.")
        };
    }

    private static ISpecification<TEntity> BuildFullTextSearch<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("searchTerm", out var searchTermObj) ||
            !filter.CustomParameters.TryGetValue("fields", out var fieldsObj))
        {
            throw new ArgumentException("SearchTerm and Fields must be provided for FullTextSearch.");
        }

        var searchTerm = searchTermObj as string;
        var fields = fieldsObj as IEnumerable<string>;

        if (string.IsNullOrEmpty(searchTerm) || fields == null || !fields.Any())
        {
            throw new ArgumentException("Invalid searchTerm or Fields for FullTextSearch.");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        Expression combinedExpression = Expression.Constant(false);

        foreach (var field in fields)
        {
            var property = Expression.Property(parameter, field);
            var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)]);
            var searchTermExpression = Expression.Constant(searchTerm);
            var containsExpression = Expression.Call(property, containsMethod, searchTermExpression);
            combinedExpression = Expression.OrElse(combinedExpression, containsExpression);
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(combinedExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildDateRange<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("startDate", out var startDateObj) ||
            !filter.CustomParameters.TryGetValue("endDate", out var endDateObj))
        {
            throw new ArgumentException("Field, StartDate, and EndDate must be provided for DateRange filter.");
        }

        var field = fieldObj as string;
        var startDateString = startDateObj as string;
        var endDateString = endDateObj as string;

        // Get inclusive parameter with default true if not specified
        var inclusive = !filter.CustomParameters.TryGetValue("inclusive", out var inclusiveObj) ||
            inclusiveObj is not bool inclusiveValue ||
            inclusiveValue;

        if (string.IsNullOrEmpty(field) || (string.IsNullOrEmpty(startDateString) && string.IsNullOrEmpty(endDateString)))
        {
            throw new ArgumentException("Invalid field or date range for DateRange filter.");
        }

        var startDateParsed = startDateString.TryParseDateOrEpoch(out var startDate);
        var endDateParsed = endDateString.TryParseDateOrEpoch(out var endDate);

        if (!startDateParsed)
        {
            throw new ArgumentException($"Invalid start date format: {startDateString}");
        }

        if (!endDateParsed)
        {
            throw new ArgumentException($"Invalid end date format: {endDateString}");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);

        Expression startComparison = inclusive
            ? Expression.GreaterThanOrEqual(property, Expression.Constant(startDate))
            : Expression.GreaterThan(property, Expression.Constant(startDate));

        Expression endComparison = inclusive
            ? Expression.LessThanOrEqual(property, Expression.Constant(endDate))
            : Expression.LessThan(property, Expression.Constant(endDate));

        Expression dateRangeExpression = Expression.AndAlso(startComparison, endComparison);

        var lambda = Expression.Lambda<Func<TEntity, bool>>(dateRangeExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildDateRelative<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("unit", out var unitObj) ||
            !filter.CustomParameters.TryGetValue("amount", out var amountObj) ||
            !filter.CustomParameters.TryGetValue("direction", out var directionObj))
        {
            throw new ArgumentException("Field, Unit, Amount, and Direction must be provided for DateRelative filter.");
        }

        var field = fieldObj as string;
        var unit = (unitObj as string)?.ToLowerInvariant();
        var amount = Convert.ToInt32(amountObj);
        var direction = (directionObj as string)?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(field) ||
            string.IsNullOrWhiteSpace(unit) ||
            string.IsNullOrWhiteSpace(direction))
        {
            throw new ArgumentException("Invalid parameters for DateRelative filter.");
        }

        if (!new[] { "day", "week", "month", "year" }.Contains(unit))
        {
            throw new ArgumentException("Unit must be one of: day, week, month, year");
        }

        if (!new[] { "past", "future" }.Contains(direction))
        {
            throw new ArgumentException("Direction must be either 'past' or 'future'");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);
        var propertyType = property.Type;

        // Handle nullable DateTime properties
        var isNullable = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        var dateProperty = isNullable ? Expression.Property(property, "Value") : property;

        // Calculate the reference date
        var now = DateTime.UtcNow;
        var referenceDate = direction == "past"
            ? GetPastDate(now, unit, amount)
            : GetFutureDate(now, unit, amount);

        // Create the date comparison expression
        var dateConstant = Expression.Constant(referenceDate);
        Expression dateComparisonExpression;

        if (direction == "past")
        {
            dateComparisonExpression = Expression.GreaterThanOrEqual(dateProperty, dateConstant);
        }
        else
        {
            dateComparisonExpression = Expression.LessThanOrEqual(dateProperty, dateConstant);
        }

        // For nullable properties, add null check
        if (isNullable)
        {
            var notNullCheck = Expression.NotEqual(property, Expression.Constant(null, propertyType));
            dateComparisonExpression = Expression.AndAlso(notNullCheck, dateComparisonExpression);
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(dateComparisonExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildNumericRange<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("min", out var minValueObj) ||
            !filter.CustomParameters.TryGetValue("max", out var maxValueObj))
        {
            throw new ArgumentException("PropertyName, MinValue, and MaxValue must be provided for NumericRange filter.");
        }

        var field = fieldObj as string;
        var minValue = Convert.ToDouble(minValueObj);
        var maxValue = Convert.ToDouble(maxValueObj);

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);
        var convertedProperty = Expression.Convert(property, typeof(double));

        var greaterThanOrEqual = Expression.GreaterThanOrEqual(convertedProperty, Expression.Constant(minValue));
        var lessThanOrEqual = Expression.LessThanOrEqual(convertedProperty, Expression.Constant(maxValue));
        var rangeExpression = Expression.AndAlso(greaterThanOrEqual, lessThanOrEqual);

        var lambda = Expression.Lambda<Func<TEntity, bool>>(rangeExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildIsNull<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj))
        {
            throw new ArgumentException("PropertyName must be provided for ExistenceCheck filter.");
        }

        var field = fieldObj as string;

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);
        var notNullExpression = Expression.Equal(property, Expression.Constant(null));

        var lambda = Expression.Lambda<Func<TEntity, bool>>(notNullExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildIsNotNull<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj))
        {
            throw new ArgumentException("PropertyName must be provided for ExistenceCheck filter.");
        }

        var field = fieldObj as string;

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);
        var notNullExpression = Expression.NotEqual(property, Expression.Constant(null));

        var lambda = Expression.Lambda<Func<TEntity, bool>>(notNullExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildTimeRange<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("startTime", out var startTimeObj) ||
            !filter.CustomParameters.TryGetValue("endTime", out var endTimeObj))
        {
            throw new ArgumentException("Field, StartTime, and EndTime must be provided for TimeRange filter.");
        }

        var field = fieldObj as string;
        var startTimeString = startTimeObj as string;
        var endTimeString = endTimeObj as string;

        // Get inclusive parameter with default true if not specified
        var inclusive = !filter.CustomParameters.TryGetValue("inclusive", out var inclusiveObj) ||
            inclusiveObj is not bool inclusiveValue ||
            inclusiveValue;

        if (!startTimeString.TryParseTime(out var startTime) || !endTimeString.TryParseTime(out var endTime))
        {
            throw new ArgumentException("StartTime and EndTime must be valid time strings in either 24-hour format (HH:mm:ss, HH:mm) or 12-hour format (hh:mm:ss tt, hh:mm tt)");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);

        // Convert the property to TimeSpan if it's a DateTime
        var convertedProperty = typeof(TEntity).GetProperty(field).PropertyType == typeof(DateTime)
            ? Expression.Property(property, "TimeOfDay")
            : property;

        Expression timeRangeExpression;

        if (startTime <= endTime)
        {
            // Normal range (e.g., 9:00 AM to 5:00 PM)
            Expression startComparison = inclusive
                ? Expression.GreaterThanOrEqual(convertedProperty, Expression.Constant(startTime))
                : Expression.GreaterThan(convertedProperty, Expression.Constant(startTime));

            Expression endComparison = inclusive
                ? Expression.LessThanOrEqual(convertedProperty, Expression.Constant(endTime))
                : Expression.LessThan(convertedProperty, Expression.Constant(endTime));

            timeRangeExpression = Expression.AndAlso(startComparison, endComparison);
        }
        else
        {
            // Overnight range (e.g., 10:00 PM to 6:00 AM)
            Expression startComparison = inclusive
                ? Expression.GreaterThanOrEqual(convertedProperty, Expression.Constant(startTime))
                : Expression.GreaterThan(convertedProperty, Expression.Constant(startTime));

            Expression endComparison = inclusive
                ? Expression.LessThanOrEqual(convertedProperty, Expression.Constant(endTime))
                : Expression.LessThan(convertedProperty, Expression.Constant(endTime));

            var lessThan = Expression.LessThan(convertedProperty, Expression.Constant(TimeSpan.FromHours(24)));
            var greaterThanOrEqualZero = Expression.GreaterThanOrEqual(convertedProperty, Expression.Constant(TimeSpan.Zero));

            var firstPart = Expression.AndAlso(startComparison, lessThan);
            var secondPart = Expression.AndAlso(greaterThanOrEqualZero, endComparison);
            timeRangeExpression = Expression.OrElse(firstPart, secondPart);
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(timeRangeExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildTimeRelative<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("unit", out var unitObj) ||
            !filter.CustomParameters.TryGetValue("amount", out var amountObj) ||
            !filter.CustomParameters.TryGetValue("direction", out var directionObj))
        {
            throw new ArgumentException("Field, Unit, Amount, and Direction must be provided for TimeRelative filter.");
        }

        var field = fieldObj as string;
        var unit = (unitObj as string)?.ToLowerInvariant();
        var amount = Convert.ToInt32(amountObj);
        var direction = (directionObj as string)?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(field) ||
            string.IsNullOrWhiteSpace(unit) ||
            string.IsNullOrWhiteSpace(direction))
        {
            throw new ArgumentException("Invalid parameters for TimeRelative filter.");
        }

        if (!new[] { "minute", "hour" }.Contains(unit))
        {
            throw new ArgumentException("Unit must be either 'minute' or 'hour'");
        }

        if (!new[] { "past", "future" }.Contains(direction))
        {
            throw new ArgumentException("Direction must be either 'past' or 'future'");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);
        var propertyType = property.Type;

        // Check if the property is TimeSpan or DateTime
        if (propertyType != typeof(TimeSpan) &&
            propertyType != typeof(TimeSpan?) &&
            propertyType != typeof(DateTime) &&
            propertyType != typeof(DateTime?))
        {
            throw new ArgumentException($"Property type must be TimeSpan or DateTime, but was {propertyType.Name}");
        }

        var isNullable = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        var isDateTime = propertyType == typeof(DateTime) || propertyType == typeof(DateTime?);
        var referenceTime = GetReferenceTime(DateTime.UtcNow, unit, amount, direction, isDateTime);

        Expression timeConstant;
        Expression timeProperty = isNullable
            ? Expression.Property(property, "Value")
            : property;
        if (isDateTime) // For DateTime, we need to compare the TimeOfDay part
        {
            timeProperty = Expression.Property(timeProperty, "TimeOfDay");
            timeConstant = Expression.Constant(((DateTime)referenceTime).TimeOfDay);
        }
        else // TimeSpan
        {
            timeConstant = Expression.Constant((TimeSpan)referenceTime);
        }

        Expression timeComparisonExpression = direction == "past"
            ? Expression.GreaterThanOrEqual(timeProperty, timeConstant)
            : Expression.LessThanOrEqual(timeProperty, timeConstant);

        // For nullable properties, add null check
        if (isNullable)
        {
            var notNullCheck = Expression.NotEqual(property, Expression.Constant(null, propertyType));
            timeComparisonExpression = Expression.AndAlso(notNullCheck, timeComparisonExpression);
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(timeComparisonExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildEnumValues<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("values", out var valuesObj))
        {
            throw new ArgumentException("Field and Values must be provided for EnumFilter.");
        }

        var field = fieldObj as string;
        var valuesString = valuesObj as string;

        if (string.IsNullOrWhiteSpace(valuesString))
        {
            throw new ArgumentException("EnumValues must be a non-empty string of semicolon-separated enum values or integers.");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);
        var enumType = property.Type;
        var underlyingType = Enum.GetUnderlyingType(enumType);

        var enumValues = valuesString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(v =>
            {
                v = v.Trim();
                if (int.TryParse(v, out var intValue))
                {
                    return Enum.ToObject(enumType, intValue);
                }

                return Enum.Parse(enumType, v);
            })
            .ToList();

        if (!enumValues.Any())
        {
            throw new ArgumentException("No valid enum values provided.");
        }

        // Convert enum values to their underlying type (usually int)
        var convertedEnumValues = enumValues.Select(e => Convert.ChangeType(e, underlyingType)).ToList();

        // Create a typed list to match the expected input type of the Contains method
        var listType = typeof(List<>).MakeGenericType(underlyingType);
        var typedList = Activator.CreateInstance(listType);
        var addMethod = listType.GetMethod("Add");
        foreach (var value in convertedEnumValues)
        {
            addMethod.Invoke(typedList, [value]);
        }

        var valuesExpression = Expression.Constant(typedList);

        // Use the Contains method to check if the property value is in the list of enum values
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(underlyingType);

        var containsExpression = Expression.Call(null, containsMethod, valuesExpression, Expression.Convert(property, underlyingType));

        var lambda = Expression.Lambda<Func<TEntity, bool>>(containsExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildTextIn<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("values", out var valuesObj))
        {
            throw new ArgumentException("Field and Values must be provided for TextIn custom filter.");
        }

        var field = fieldObj as string;
        var valuesString = valuesObj as string;

        if (string.IsNullOrWhiteSpace(valuesString))
        {
            throw new ArgumentException("Values must be a non-empty string of semicolon-separated text values.");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);

        // Split the values and create a list
        var values = valuesString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (!values.Any())
        {
            throw new ArgumentException("No valid text values provided.");
        }

        // Create a typed list of strings
        var valuesExpression = Expression.Constant(values);

        // Use the Contains method to check if the property value is in the list
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));

        var containsExpression = Expression.Call(null, containsMethod, valuesExpression, property);

        var lambda = Expression.Lambda<Func<TEntity, bool>>(containsExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildTextNotIn<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("values", out var valuesObj))
        {
            throw new ArgumentException("Field and Values must be provided for TextNotIn custom filter.");
        }

        var field = fieldObj as string;
        var valuesString = valuesObj as string;

        if (string.IsNullOrWhiteSpace(valuesString))
        {
            throw new ArgumentException("Values must be a non-empty string of semicolon-separated text values.");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);

        // Split the values and create a list
        var values = valuesString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (!values.Any())
        {
            throw new ArgumentException("No valid text values provided.");
        }

        // Create a typed list of strings
        var valuesExpression = Expression.Constant(values);

        // Use the Contains method and negate it
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));

        var containsExpression = Expression.Call(null, containsMethod, valuesExpression, property);
        var notInExpression = Expression.Not(containsExpression);

        var lambda = Expression.Lambda<Func<TEntity, bool>>(notInExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildNumericIn<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("values", out var valuesObj))
        {
            throw new ArgumentException("Field and Values must be provided for NumericIn custom filter.");
        }

        var field = fieldObj as string;
        var valuesString = valuesObj as string;

        if (string.IsNullOrWhiteSpace(valuesString))
        {
            throw new ArgumentException("Values must be a non-empty string of semicolon-separated numeric values.");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);
        var propertyType = property.Type;

        // Handle nullable types
        var isNullable = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        var underlyingType = isNullable ? Nullable.GetUnderlyingType(propertyType) : propertyType;

        // Parse the numeric values
        var values = valuesString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(v =>
            {
                v = v.Trim();

                return Convert.ChangeType(v, underlyingType, CultureInfo.InvariantCulture);
            })
            .ToList();

        if (!values.Any())
        {
            throw new ArgumentException("No valid numeric values provided.");
        }

        // Create a typed list for the numeric values
        var listType = typeof(List<>).MakeGenericType(underlyingType);
        var typedList = Activator.CreateInstance(listType);
        var addMethod = listType.GetMethod("Add");
        foreach (var value in values)
        {
            addMethod.Invoke(typedList, [value]);
        }

        var valuesExpression = Expression.Constant(typedList);

        // Use the Contains method
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(underlyingType);

        var propertyValue = isNullable
            ? Expression.Property(property, "Value")
            : property;

        var containsExpression = Expression.Call(null, containsMethod, valuesExpression, propertyValue);

        var lambda = Expression.Lambda<Func<TEntity, bool>>(containsExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static ISpecification<TEntity> BuildNumericNotIn<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (!filter.CustomParameters.TryGetValue("field", out var fieldObj) ||
            !filter.CustomParameters.TryGetValue("values", out var valuesObj))
        {
            throw new ArgumentException("Field and Values must be provided for NumericNotIn custom filter.");
        }

        var field = fieldObj as string;
        var valuesString = valuesObj as string;

        if (string.IsNullOrWhiteSpace(valuesString))
        {
            throw new ArgumentException("Values must be a non-empty string of semicolon-separated numeric values.");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, field);
        var propertyType = property.Type;

        // Handle nullable types
        var isNullable = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        var underlyingType = isNullable ? Nullable.GetUnderlyingType(propertyType) : propertyType;

        // Parse the numeric values
        var values = valuesString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(v =>
            {
                v = v.Trim();

                return Convert.ChangeType(v, underlyingType, CultureInfo.InvariantCulture);
            })
            .ToList();

        if (!values.Any())
        {
            throw new ArgumentException("No valid numeric values provided.");
        }

        // Create a typed list for the numeric values
        var listType = typeof(List<>).MakeGenericType(underlyingType);
        var typedList = Activator.CreateInstance(listType);
        var addMethod = listType.GetMethod("Add");
        foreach (var value in values)
        {
            addMethod.Invoke(typedList, [value]);
        }

        var valuesExpression = Expression.Constant(typedList);

        // Use the Contains method and negate it
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(underlyingType);

        var propertyValue = isNullable
            ? Expression.Property(property, "Value")
            : property;

        var containsExpression = Expression.Call(null, containsMethod, valuesExpression, propertyValue);
        var notInExpression = Expression.Not(containsExpression);

        var lambda = Expression.Lambda<Func<TEntity, bool>>(notInExpression, parameter);

        return new Specification<TEntity>(lambda);
    }

    private static DateTime GetPastDate(DateTime now, string unit, int amount) // TODO: move to DateTimeExtensions
    {
        return unit switch
        {
            "day" => now.AddDays(-amount),
            "week" => now.AddDays(-amount * 7),
            "month" => now.AddMonths(-amount),
            "year" => now.AddYears(-amount),
            _ => throw new ArgumentException("Invalid date unit")
        };
    }

    private static DateTime GetFutureDate(DateTime now, string unit, int amount) // TODO: move to DateTimeExtensions
    {
        return unit switch
        {
            "day" => now.AddDays(amount),
            "week" => now.AddDays(amount * 7),
            "month" => now.AddMonths(amount),
            "year" => now.AddYears(amount),
            _ => throw new ArgumentException("Invalid date unit")
        };
    }

    private static object GetReferenceTime(DateTime now, string unit, int amount, string direction, bool returnDateTime)
    {
        var multiplier = direction == "past" ? -1 : 1;

        var resultDateTime = unit switch
        {
            "minute" => now.AddMinutes(amount * multiplier),
            "hour" => now.AddHours(amount * multiplier),
            _ => throw new ArgumentException("Invalid time unit")
        };

        return returnDateTime ? resultDateTime : resultDateTime.TimeOfDay;
    }
}