// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Collections.Generic;
using System.Linq;

public abstract class Enumeration<TValue>
    : Enumeration<int, TValue>, IEnumeration<TValue>
    where TValue : IComparable
{
    protected Enumeration(int id, TValue value)
        : base(id, value)
    {
    }

    public static new TEnumeration FromId<TEnumeration>(int id)
        where TEnumeration : IEnumeration<TValue>
    {
        return Enumeration<int, TValue>.FromId<TEnumeration>(id);
    }

    public static new TEnumeration FromValue<TEnumeration>(TValue value)
        where TEnumeration : IEnumeration<TValue>
    {
        return Parse<TEnumeration, TValue>(value, "value", i => i.Value.Equals(value));
    }

    public static new IEnumerable<TEnumeration> GetAll<TEnumeration>()
        where TEnumeration : IEnumeration<TValue>
    {
        return Enumeration<int, TValue>.GetAll<TEnumeration>();
    }

    private static TEnumeration Parse<TEnumeration, TSearch>(TSearch searchValue, string description, Func<TEnumeration, bool> predicate)
        where TEnumeration : IEnumeration<TValue>
    {
        return GetAll<TEnumeration>().FirstOrDefault(predicate)
            ?? throw new InvalidOperationException($"'{searchValue}' is not a valid {description} for {typeof(TEnumeration)}");
    }
}
