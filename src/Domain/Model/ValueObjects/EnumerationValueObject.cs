// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Reflection;

// Better use the new Enumeration (non ValueObject) implementation
/// <summary>
/// Represents enumeration value object.
/// </summary>
/// <typeparam name="TEnumeration">The enumeration type.</typeparam>
/// <typeparam name="TKey">The key type.</typeparam>
public abstract class EnumerationValueObject<TEnumeration, TKey> : ValueObject
    where TEnumeration : EnumerationValueObject<TEnumeration, TKey>
    where TKey : struct
{
    private static readonly Dictionary<TKey, TEnumeration> ByKey = GetEnumerations().ToDictionary(e => e.Key);

    private static readonly Dictionary<string, TEnumeration> ByName = GetEnumerations().ToDictionary(e => e.Name);

    private int? cachedHashCode;

#pragma warning disable SA1202 // Elements should be ordered by access
    /// <summary>
    /// Stores the all.
    /// </summary>
    public static IReadOnlyCollection<TEnumeration> All = ByKey.Values.OfType<TEnumeration>().ToList();
#pragma warning restore SA1202 // Elements should be ordered by access

    /// <summary>
    /// Initializes a new instance of the <c>EnumerationValueObject</c> class.
    /// </summary>
    /// <param name="key">The key used by the operation.</param>
    /// <param name="name">The name of the value.</param>
    protected EnumerationValueObject(TKey key, string name)
    {
        EnsureArg.IsNotDefault(key, nameof(key));
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));

        this.Key = key;
        this.Name = name;
    }

    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public TKey Key { get; protected set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// Executes the operator == operation.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(EnumerationValueObject<TEnumeration, TKey> left, TKey right)
    {
        if (left is null)
        {
            return false;
        }

        return left.Key.Equals(right);
    }

    /// <summary>
    /// Executes the operator != operation.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(EnumerationValueObject<TEnumeration, TKey> left, TKey right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Executes the operator == operation.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(TKey left, EnumerationValueObject<TEnumeration, TKey> right)
    {
        return right == left;
    }

    /// <summary>
    /// Executes the operator != operation.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(TKey left, EnumerationValueObject<TEnumeration, TKey> right)
    {
        return !(right == left);
    }

    /// <summary>
    /// Creates .
    /// </summary>
    /// <param name="key">The key used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TEnumeration Create(TKey key)
    {
        return ByKey.ContainsKey(key) ? ByKey[key] : default;
    }

    /// <summary>
    /// Creates .
    /// </summary>
    /// <param name="name">The name of the value.</param>
    /// <returns>The result of the operation.</returns>
    public static TEnumeration Create(string name)
    {
        return ByName.ContainsKey(name) ? ByName[name] : default;
    }

    /// <summary>
    /// Determines whether is.
    /// </summary>
    /// <param name="name">The name of the value.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool Is(string name)
    {
        return All.Select(e => e.Name).Contains(name);
    }

    /// <summary>
    /// Determines whether is.
    /// </summary>
    /// <param name="key">The key used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool Is(TKey key)
    {
        return All.Select(e => e.Key).Contains(key);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Name;
    }

    /// <inheritdoc/>
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

/// <summary>
/// Represents enumeration value object.
/// </summary>
/// <typeparam name="TEnumeration">The enumeration type.</typeparam>
public abstract class EnumerationValueObject<TEnumeration> : ValueObject
    where TEnumeration : EnumerationValueObject<TEnumeration>
{
    private static readonly Dictionary<string, TEnumeration> Enumerations = GetEnumerations().ToDictionary(e => e.Key);

    private int? cachedHashCode;

#pragma warning disable SA1202 // Elements should be ordered by access
    /// <summary>
    /// Stores the all.
    /// </summary>
    public static IReadOnlyCollection<TEnumeration> All = Enumerations.Values.OfType<TEnumeration>().ToList();
#pragma warning restore SA1202 // Elements should be ordered by access

    /// <summary>
    /// Initializes a new instance of the <c>EnumerationValueObject</c> class.
    /// </summary>
    /// <param name="key">The key used by the operation.</param>
    protected EnumerationValueObject(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("The enum key cannot be null or empty");
        }

        this.Key = key;
    }

    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public virtual string Key { get; protected set; }

    /// <summary>
    /// Executes the operator == operation.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Executes the operator != operation.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(EnumerationValueObject<TEnumeration> left, string right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Executes the operator == operation.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(string left, EnumerationValueObject<TEnumeration> right)
    {
        return right == left;
    }

    /// <summary>
    /// Executes the operator != operation.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type.</typeparam>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(string left, EnumerationValueObject<TEnumeration> right)
    {
        return !(right == left);
    }

    /// <summary>
    /// Determines whether is.
    /// </summary>
    /// <param name="key">The key used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool Is(string key)
    {
        return All.Select(e => e.Key).Contains(key);
    }

    /// <summary>
    /// Executes the from key operation.
    /// </summary>
    /// <param name="key">The key used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TEnumeration FromKey(string key)
    {
        return Enumerations.ContainsKey(key) ? Enumerations[key] : default;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return this.cachedHashCode ??= this.GetAtomicValues()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Key;
    }

    /// <inheritdoc/>
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
