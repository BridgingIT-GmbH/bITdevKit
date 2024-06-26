﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public abstract class Enumeration(int id, string name) : IComparable // TODO: or use https://github.com/ardalis/SmartEnum (better webapi support)
{
    public int Id { get; private set; } = id;

    public string Name { get; private set; } = name;

    public static IEnumerable<T> GetAll<T>()
        where T : Enumeration
    {
        return typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null)).Cast<T>();
    }

    public static T From<T>(int id)
        where T : Enumeration
    {
        return Parse(id, "id", (Func<T, bool>)(i => i.Id == id));
    }

    public static T From<T>(string name)
        where T : Enumeration
    {
        return Parse(name, "name", (Func<T, bool>)(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
    }

    public override string ToString() => this.Name;

    public override bool Equals(object obj)
    {
        var otherValue = obj as Enumeration;
        if (otherValue is null)
        {
            return false;
        }

        var typeMatches = this.GetType().Equals(obj.GetType());
        var valueMatches = this.Id.Equals(otherValue.Id);

        return typeMatches && valueMatches;
    }

    public override int GetHashCode() => this.Id.GetHashCode();

    public int CompareTo(object other) => this.Id.CompareTo(((Enumeration)other).Id);

    private static T Parse<T, TValue>(TValue value, string description, Func<T, bool> predicate)
        where T : Enumeration
    {
        var item = GetAll<T>().FirstOrDefault(predicate);
        if (item is null)
        {
            throw new InvalidOperationException($"'{value}' is not a valid {description} for {typeof(T)}");
        }

        return item;
    }
}