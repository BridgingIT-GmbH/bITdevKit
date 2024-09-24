// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Reflection;

// Better use the new Enumeration (non ValueObject) implementation
public abstract class EnumerationValueObject<TEnumeration, TKey> : ValueObject
    where TEnumeration : EnumerationValueObject<TEnumeration, TKey>
    where TKey : struct
{
    private static readonly Dictionary<TKey, TEnumeration> ByKey = GetEnumerations().ToDictionary(e => e.Key);

    private static readonly Dictionary<string, TEnumeration> ByName = GetEnumerations().ToDictionary(e => e.Name);

    private int? cachedHashCode;

#pragma warning disable SA1202 // Elements should be ordered by access
    public static IReadOnlyCollection<TEnumeration> All = ByKey.Values.OfType<TEnumeration>().ToList();
#pragma warning restore SA1202 // Elements should be ordered by access

    protected EnumerationValueObject(TKey key, string name)
    {
        EnsureArg.IsNotDefault(key, nameof(key));
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));

        this.Key = key;
        this.Name = name;
    }

    public TKey Key { get; protected set; }

    public string Name { get; protected set; }

    public static bool operator ==(EnumerationValueObject<TEnumeration, TKey> left, TKey right)
    {
        if (left is null)
        {
            return false;
        }

        return left.Key.Equals(right);
    }

    public static bool operator !=(EnumerationValueObject<TEnumeration, TKey> left, TKey right)
    {
        return !(left == right);
    }

    public static bool operator ==(TKey left, EnumerationValueObject<TEnumeration, TKey> right)
    {
        return right == left;
    }

    public static bool operator !=(TKey left, EnumerationValueObject<TEnumeration, TKey> right)
    {
        return !(right == left);
    }

    public static TEnumeration Create(TKey key)
    {
        return ByKey.ContainsKey(key) ? ByKey[key] : default;
    }

    public static TEnumeration Create(string name)
    {
        return ByName.ContainsKey(name) ? ByName[name] : default;
    }

    public static bool Is(string name)
    {
        return All.Select(e => e.Name).Contains(name);
    }

    public static bool Is(TKey key)
    {
        return All.Select(e => e.Key).Contains(key);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (GetUnproxiedType(this) != GetUnproxiedType(obj))
        {
            return false;
        }

        return this.GetAtomicValues()
            .SequenceEqual(((EnumerationValueObject<TEnumeration, TKey>)obj).GetAtomicValues());
    }

    /// <summary>
    ///     Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    ///     A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
    /// </returns>
    public override int GetHashCode()
    {
        return this.cachedHashCode ??= this.GetAtomicValues()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    public override string ToString()
    {
        return this.Name;
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Key;
    }

    private static TEnumeration[] GetEnumerations()
    {
        return typeof(TEnumeration).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(info => info.FieldType == typeof(TEnumeration))
            .Select(info => (TEnumeration)info.GetValue(null))
            .ToArray();
    }
}

public abstract class EnumerationValueObject<TEnumeration> : ValueObject
    where TEnumeration : EnumerationValueObject<TEnumeration>
{
    private static readonly Dictionary<string, TEnumeration> Enumerations = GetEnumerations().ToDictionary(e => e.Key);

    private int? cachedHashCode;

#pragma warning disable SA1202 // Elements should be ordered by access
    public static IReadOnlyCollection<TEnumeration> All = Enumerations.Values.OfType<TEnumeration>().ToList();
#pragma warning restore SA1202 // Elements should be ordered by access

    protected EnumerationValueObject(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("The enum key cannot be null or empty");
        }

        this.Key = key;
    }

    public virtual string Key { get; protected set; }

    public static bool operator ==(EnumerationValueObject<TEnumeration> left, string right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Key.Equals(right);
    }

    public static bool operator !=(EnumerationValueObject<TEnumeration> left, string right)
    {
        return !(left == right);
    }

    public static bool operator ==(string left, EnumerationValueObject<TEnumeration> right)
    {
        return right == left;
    }

    public static bool operator !=(string left, EnumerationValueObject<TEnumeration> right)
    {
        return !(right == left);
    }

    public static bool Is(string key)
    {
        return All.Select(e => e.Key).Contains(key);
    }

    public static TEnumeration FromKey(string key)
    {
        return Enumerations.ContainsKey(key) ? Enumerations[key] : default;
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (GetUnproxiedType(this) != GetUnproxiedType(obj))
        {
            return false;
        }

        return this.GetAtomicValues().SequenceEqual(((EnumerationValueObject<TEnumeration>)obj).GetAtomicValues());
    }

    public override int GetHashCode()
    {
        return this.cachedHashCode ??= this.GetAtomicValues()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    public override string ToString()
    {
        return this.Key;
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Key;
    }

    private static TEnumeration[] GetEnumerations()
    {
        return typeof(TEnumeration).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(info => info.FieldType == typeof(TEnumeration))
            .Select(info => (TEnumeration)info.GetValue(null))
            .ToArray();
    }
}