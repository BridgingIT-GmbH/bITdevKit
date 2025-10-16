// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;

/// <summary>
/// Entity Framework Core value converter for simple <see cref="IEnumeration"/> implementations
/// using an <see cref="int"/> identifier. Converts the enumeration instance to its Id for persistence
/// and reconstructs the enumeration from the stored Id when materializing.
/// </summary>
/// <typeparam name="TEnumeration">The concrete enumeration type implementing <see cref="IEnumeration"/>.</typeparam>
public class EnumerationConverter<TEnumeration> : ValueConverter<TEnumeration, int>
    where TEnumeration : IEnumeration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnumerationConverter{TEnumeration}"/> class
    /// configuring conversion between the enumeration instance and its <see cref="int"/> identifier.
    /// </summary>
    public EnumerationConverter()
        : base(
            enumeration => enumeration.Id, // Enumeration -> int
            value => Enumeration.FromId<TEnumeration>(value)) // int -> Enumeration
    { }
}

/// <summary>
/// Entity Framework Core value converter for <see cref="IEnumeration{TId,TValue}"/> implementations
/// with an <see cref="int"/> identifier and an additional value payload.
/// </summary>
/// <typeparam name="TValue">The underlying value type stored inside the enumeration.</typeparam>
/// <typeparam name="TEnumeration">The concrete enumeration type.</typeparam>
public class EnumerationConverter<TValue, TEnumeration> : ValueConverter<TEnumeration, int>
    where TEnumeration : IEnumeration<int, TValue>
    where TValue : IComparable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnumerationConverter{TValue,TEnumeration}"/> class
    /// configuring conversion between the enumeration instance and its <see cref="int"/> identifier.
    /// </summary>
    public EnumerationConverter()
        : base(
            enumeration => enumeration.Id, // Enumeration -> int
            id => Enumeration<int, TValue>.FromId<TEnumeration>(id)) // int -> Enumeration
    { }
}

/// <summary>
/// Entity Framework Core value converter for fully generic <see cref="IEnumeration{TId,TValue}"/>
/// implementations with configurable identifier type.
/// </summary>
/// <typeparam name="TId">The identifier type used by the enumeration (e.g. int, string, Guid).</typeparam>
/// <typeparam name="TValue">The value payload type inside the enumeration.</typeparam>
/// <typeparam name="TEnumeration">The concrete enumeration type.</typeparam>
public class EnumerationConverter<TId, TValue, TEnumeration> : ValueConverter<TEnumeration, TId>
    where TEnumeration : IEnumeration<TId, TValue>
    where TId : IComparable
    where TValue : IComparable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnumerationConverter{TId,TValue,TEnumeration}"/> class
    /// configuring conversion between the enumeration instance and its identifier of type <typeparamref name="TId"/>.
    /// </summary>
    public EnumerationConverter()
        : base(
            enumeration => enumeration.Id, // Enumeration -> TId
            id => Enumeration<TId, TValue>.FromId<TEnumeration>(id)) // TId -> Enumeration
    { }
}
