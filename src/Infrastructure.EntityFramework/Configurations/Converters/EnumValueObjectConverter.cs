// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class EnumValueObjectConverter<TEnumeration, TKey>
    : ValueConverter<EnumerationValueObject<TEnumeration, TKey>, TKey>
    where TEnumeration : EnumerationValueObject<TEnumeration, TKey>
    where TKey : struct
{
    public EnumValueObjectConverter()
        : base(v => v.Key, v => EnumerationValueObject<TEnumeration, TKey>.Create(v)) { }
}

public class EnumValueObjectConverter<TEnumeration> : ValueConverter<EnumerationValueObject<TEnumeration>, string>
    where TEnumeration : EnumerationValueObject<TEnumeration>
{
    public EnumValueObjectConverter()
        : base(v => v.Key, v => EnumerationValueObject<TEnumeration>.FromKey(v)) { }
}