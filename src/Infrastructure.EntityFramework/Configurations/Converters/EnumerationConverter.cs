// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class EnumerationConverter<TEnumeration> : ValueConverter<TEnumeration, int>
    where TEnumeration : IEnumeration
{
    public EnumerationConverter()
        : base(
            enumeration => enumeration.Id, // Converts Enumeration to int
            value => Enumeration.FromId<TEnumeration>(value)) // Converts int back to Enumeration
    {
    }
}

public class EnumerationConverter<TValue, TEnumeration> : ValueConverter<TEnumeration, int>
    where TEnumeration : IEnumeration<int, TValue>
    where TValue : IComparable
{
    public EnumerationConverter()
        : base(
            enumeration => enumeration.Id, // Converts Enumeration to TId
            id => Enumeration<int, TValue>.FromId<TEnumeration>(id)) // Converts TId back to Enumeration
    {
    }
}

public class EnumerationConverter<TId, TValue, TEnumeration> : ValueConverter<TEnumeration, TId>
    where TEnumeration : IEnumeration<TId, TValue>
    where TId : IComparable
    where TValue : IComparable
{
    public EnumerationConverter()
        : base(
            enumeration => enumeration.Id, // Converts Enumeration to TId
            id => Enumeration<TId, TValue>.FromId<TEnumeration>(id)) // Converts TId back to Enumeration
    {
    }
}