// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Represents enum value object converter.
/// </summary>
/// <typeparam name="TEnumeration">The enumeration type.</typeparam>
/// <typeparam name="TKey">The key type.</typeparam>
public class EnumValueObjectConverter<TEnumeration, TKey>
    : ValueConverter<EnumerationValueObject<TEnumeration, TKey>, TKey>
    where TEnumeration : EnumerationValueObject<TEnumeration, TKey>
    where TKey : struct
{
    /// <summary>
    /// Initializes a new instance of the <c>EnumValueObjectConverter</c> class.
    /// </summary>
    public EnumValueObjectConverter()
        : base(v => v.Key, v => EnumerationValueObject<TEnumeration, TKey>.Create(v)) { }
}

/// <summary>
/// Represents enum value object converter.
/// </summary>
/// <typeparam name="TEnumeration">The enumeration type.</typeparam>
public class EnumValueObjectConverter<TEnumeration> : ValueConverter<EnumerationValueObject<TEnumeration>, string>
    where TEnumeration : EnumerationValueObject<TEnumeration>
{
    /// <summary>
    /// Initializes a new instance of the <c>EnumValueObjectConverter</c> class.
    /// </summary>
    public EnumValueObjectConverter()
        : base(v => v.Key, v => EnumerationValueObject<TEnumeration>.FromKey(v)) { }
}
