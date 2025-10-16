// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;

/// <summary>
/// Provides an Entity Framework Core <see cref="ValueConverter{TModel,TProvider}"/> implementation
/// to persist <see cref="DateOnly"/> values as <see cref="DateTime"/> (at midnight) for providers that
/// do not natively support <c>DateOnly</c>.
/// </summary>
/// <remarks>
/// The conversion stores the date component at 00:00:00 (midnight) using <see cref="TimeOnly.MinValue"/> to
/// guarantee a stable and lossless round-trip between model and provider types.
/// Typical usage in a model configuration:
/// <code>
/// builder.Property(e => e.BirthDate)
///        .HasConversion(new DateOnlyConverter())
///        .Metadata.SetValueComparer(new DateOnlyComparer());
/// </code>
/// Pairing the converter with <see cref="DateOnlyComparer"/> enables accurate change tracking for
/// <see cref="DateOnly"/> properties.
/// </remarks>
public class DateOnlyConverter : ValueConverter<DateOnly, DateTime>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DateOnlyConverter"/> configuring the
    /// conversion logic between <see cref="DateOnly"/> and <see cref="DateTime"/>.
    /// </summary>
    public DateOnlyConverter() : base(
        dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
        dateTime => DateOnly.FromDateTime(dateTime))
    { }
}

/// <summary>
/// Provides a <see cref="ValueComparer{T}"/> for <see cref="DateOnly"/> that compares values
/// based on their underlying <see cref="DateOnly.DayNumber"/> for efficient change tracking.
/// </summary>
/// <remarks>
/// EF Core uses value comparers to determine if a property value has changed. Comparing on
/// <see cref="DateOnly.DayNumber"/> is both fast and exact for <see cref="DateOnly"/> instances.
/// The hash code delegates to the default <see cref="DateOnly.GetHashCode"/> implementation.
/// </remarks>
public class DateOnlyComparer : ValueComparer<DateOnly>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DateOnlyComparer"/> with equality
    /// comparison and hash code generation logic for <see cref="DateOnly"/>.
    /// </summary>
    public DateOnlyComparer() : base(
        (x, y) => x.DayNumber == y.DayNumber,
        dateOnly => dateOnly.GetHashCode())
    { }
}