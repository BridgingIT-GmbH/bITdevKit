// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

// inspiration: https://codeblog.jonskeet.uk/2006/01/05/classenum/
[DebuggerDisplay("Id={Id}")]
public abstract class Enumeration<TId, TValue>(TId id, TValue value)
    : IEnumeration<TId, TValue>
    where TId : IComparable //, IEquatable<TId>
    where TValue : IComparable //, IEquatable<TValue>
{
    public TId Id { get; private set; } = id;

    public TValue Value { get; private set; } = value;

    public static IEnumerable<TEnumeration> GetAll<TEnumeration>()
        where TEnumeration : IEnumeration<TId, TValue>
    {
        return typeof(TEnumeration).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null)).Cast<TEnumeration>();
    }

    public static TEnumeration FromId<TEnumeration>(TId id)
        where TEnumeration : IEnumeration<TId, TValue>
    {
        return Parse<TEnumeration, TId>(id, "id", i => i.Id.Equals(id));
    }

    public static TEnumeration FromValue<TEnumeration>(TValue value)
        where TEnumeration : IEnumeration<TId, TValue>
    {
        return Parse<TEnumeration, TValue>(value, "value", i => i.Value.Equals(value));
    }

    public override string ToString() => this.Value.ToString();

    public override bool Equals(object obj)
    {
        if (obj is not IEnumeration<TId, TValue> otherValue)
        {
            return false;
        }

        var typeMatches = this.GetType().Equals(obj.GetType());
        var valueMatches = this.Id.Equals(otherValue.Id);

        return typeMatches && valueMatches;
    }

    public override int GetHashCode() => this.Id.GetHashCode();

    public int CompareTo(object other) => this.Id.CompareTo(((IEnumeration<TId, TValue>)other).Id);

    private static TEnumeration Parse<TEnumeration, TSearch>(TSearch searchValue, string description, Func<TEnumeration, bool> predicate)
        where TEnumeration : IEnumeration<TId, TValue>
    {
        return GetAll<TEnumeration>().FirstOrDefault(predicate)
            ?? throw new InvalidOperationException($"'{searchValue}' is not a valid {description} for {typeof(TEnumeration)}");
    }
}