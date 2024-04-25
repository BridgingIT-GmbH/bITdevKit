// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class EnumValueObjectConverter<TEnumeration, TKey>
    : ValueConverter<EnumValueObject<TEnumeration, TKey>, TKey>
    where TEnumeration : EnumValueObject<TEnumeration, TKey>
    where TKey : struct
{
    public EnumValueObjectConverter()
        : base(
            v => v.Key,
            v => EnumValueObject<TEnumeration, TKey>.ForKey(v))
    {
    }
}

public class EnumValueObjectConverter<TEnumeration>
    : ValueConverter<EnumValueObject<TEnumeration>, string>
    where TEnumeration : EnumValueObject<TEnumeration>
{
    public EnumValueObjectConverter()
        : base(
            v => v.Key,
            v => EnumValueObject<TEnumeration>.FromKey(v))
    {
    }
}