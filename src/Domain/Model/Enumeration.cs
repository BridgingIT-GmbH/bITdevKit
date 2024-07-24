// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// inspiration: https://codeblog.jonskeet.uk/2006/01/05/classenum/
[DebuggerDisplay("Id={Id}, Name={Value}")]
public abstract class Enumeration(int id, string value)
    : Enumeration<int, string>(id, value), IEnumeration
{
    public static new TEnumeration FromId<TEnumeration>(int id)
        where TEnumeration : IEnumeration
    {
        return Enumeration<int, string>.FromId<TEnumeration>(id);
    }

    public static new TEnumeration FromValue<TEnumeration>(string value)
        where TEnumeration : IEnumeration
    {
        return Parse<TEnumeration, string>(
            value,
            "value",
            e => string.Equals(e.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    public static new IEnumerable<TEnumeration> GetAll<TEnumeration>()
        where TEnumeration : IEnumeration
    {
        return Enumeration<int, string>.GetAll<TEnumeration>();
    }

    private static TEnumeration Parse<TEnumeration, TSearch>(TSearch searchValue, string description, Func<TEnumeration, bool> predicate)
        where TEnumeration : IEnumeration
    {
        return GetAll<TEnumeration>().FirstOrDefault(predicate)
            ?? throw new InvalidOperationException($"'{searchValue}' is not a valid {description} for {typeof(TEnumeration)}");
    }
}