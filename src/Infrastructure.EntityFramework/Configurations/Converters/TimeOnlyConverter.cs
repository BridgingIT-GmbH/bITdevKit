// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;

/// <summary>
/// Entity Framework Core value converter for persisting <see cref="TimeOnly"/> values
/// as <see cref="TimeSpan"/> to ensure broad database provider compatibility.
/// </summary>
/// <remarks>
/// Stores a <see cref="TimeOnly"/> as a <see cref="TimeSpan"/> (duration since midnight) because not all
/// database providers offer native <c>TIME</c>/<c>TimeOnly</c> support yet.
/// </remarks>
public class TimeOnlyConverter : ValueConverter<TimeOnly, TimeSpan>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOnlyConverter"/> class configuring
    /// conversion between <see cref="TimeOnly"/> and <see cref="TimeSpan"/>.
    /// </summary>
    public TimeOnlyConverter() : base(
        timeOnly => timeOnly.ToTimeSpan(),
        timeSpan => TimeOnly.FromTimeSpan(timeSpan))
    { }
}

/// <summary>
/// Entity Framework Core value comparer for <see cref="TimeOnly"/> values to enable proper
/// change tracking and caching behavior.
/// </summary>
/// <remarks>
/// EF Core needs a <see cref="ValueComparer{T}"/> for non-primitive types to determine if a property changed.
/// This implementation compares the underlying <see cref="TimeOnly.Ticks"/> to provide fast, precise equality
/// without culture or time zone concerns and supplies a stable hash code for dictionary/set usage.
/// Usage example when configuring a property:
/// <code>
/// builder.Property(e => e.StartTime)
///        .HasConversion(new TimeOnlyConverter())
///        .Metadata.SetValueComparer(new TimeOnlyComparer());
/// </code>
/// </remarks>
public class TimeOnlyComparer : ValueComparer<TimeOnly>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOnlyComparer"/> class using tick-based
    /// equality and the default hash code implementation.
    /// </summary>
    public TimeOnlyComparer() : base(
        (x, y) => x.Ticks == y.Ticks,
        timeOnly => timeOnly.GetHashCode())
    { }
}