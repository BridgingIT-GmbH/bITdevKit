// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static class ConditionalCollectionExtensions
{
    /// <summary>
    /// Adds an item to the collection if the specified condition is true.
    /// If the collection is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to add to, or null.</param>
    /// <param name="item">The item to add.</param>
    /// <param name="condition">The condition that must be true to add the item.</param>
    /// <returns>The collection (for chaining if desired), or null if the collection was null.</returns>
    /// <example>
    /// var orderItems = new List<Item>();
    /// var item = new Item { Price = 10.0m };
    /// orderItems.AddIf(item, item.Price > 5.0m); // Adds item if its price is greater than 5
    /// orderItems = null;
    /// orderItems.AddIf(item, true); // Does nothing and returns null silently
    /// </example>
    public static ICollection<T> AddIf<T>(this ICollection<T> collection, T item, bool condition)
    {
        if (collection == null)
        {
            return null;
        }

        if (condition)
        {
            collection.Add(item);
        }
        return collection;
    }

    /// <summary>
    /// Adds an item to the collection if the specified predicate returns true for the item.
    /// If the collection is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to add to, or null.</param>
    /// <param name="item">The item to add.</param>
    /// <param name="predicate">The predicate that determines if the item should be added.</param>
    /// <returns>The collection (for chaining if desired), or null if the collection was null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null and the collection is not null.</exception>
    /// <example>
    /// var orderItems = new List<Item>();
    /// var item = new Item { Quantity = 3 };
    /// orderItems.AddIf(item, x => x.Quantity > 0); // Adds item if its quantity is positive
    /// orderItems = null;
    /// orderItems.AddIf(item, x => x.Quantity > 0); // Does nothing and returns null silently
    /// </example>
    public static ICollection<T> AddIf<T>(this ICollection<T> collection, T item, Func<T, bool> predicate)
    {
        if (collection == null)
        {
            return null;
        }

        if (predicate != null && predicate(item))
        {
            collection.Add(item);
        }
        return collection;
    }

    /// <summary>
    /// Adds an item to the collection if it is not null.
    /// If the collection is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to add to, or null.</param>
    /// <param name="item">The item to add.</param>
    /// <returns>The collection (for chaining if desired), or null if the collection was null.</returns>
    /// <example>
    /// var orderItems = new List<Item>();
    /// Item filteredItem = null; // Could be result of some operation
    /// orderItems.AddIfNotNull(filteredItem); // Won’t add because it’s null
    /// filteredItem = new Item { Price = 15.0m };
    /// orderItems.AddIfNotNull(filteredItem); // Adds because it’s not null
    /// orderItems = null;
    /// orderItems.AddIfNotNull(filteredItem); // Does nothing and returns null silently
    /// </example>
    public static ICollection<T> AddIfNotNull<T>(this ICollection<T> collection, T item)
    {
        if (collection == null)
        {
            return null;
        }

        if (item != null)
        {
            collection.Add(item);
        }
        return collection;
    }

    /// <summary>
    /// Adds a range of items to the collection if the specified condition is true.
    /// If the collection is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to add to, or null.</param>
    /// <param name="items">The items to add.</param>
    /// <param name="condition">The condition that must be true to add the items.</param>
    /// <returns>The collection (for chaining if desired), or null if the collection was null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null and the collection is not null.</exception>
    /// <example>
    /// var orderItems = new List<Item>();
    /// var items = new[] { new Item { Price = 20.0m }, new Item { Price = 5.0m } };
    /// orderItems.AddRangeIf(items, items.Any(i => i.Price > 10.0m)); // Adds all if any price > 10
    /// orderItems = null;
    /// orderItems.AddRangeIf(items, true); // Does nothing and returns null silently
    /// </example>
    public static ICollection<T> AddRangeIf<T>(this ICollection<T> collection, IEnumerable<T> items, bool condition)
    {
        if (collection == null)
        {
            return null;
        }

        if (condition)
        {
            foreach (var item in items.SafeNull())
            {
                collection.Add(item);
            }
        }
        return collection;
    }

    /// <summary>
    /// Adds an item to the collection if it doesn’t already exist, based on equality or a custom comparer.
    /// If the collection is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to add to, or null.</param>
    /// <param name="item">The item to add.</param>
    /// <param name="comparer">The comparer to determine uniqueness (defaults to EqualityComparer<T>.Default).</param>
    /// <returns>The collection (for chaining if desired), or null if the collection was null.</returns>
    /// <example>
    /// var orderItems = new List<Item> { new Item { Id = 1 } };
    /// var newItem = new Item { Id = 1 };
    /// orderItems.AddIfUnique(newItem, Comparer<Item>.Create((a, b) => a.Id.CompareTo(b.Id))); // Won’t add if Id matches
    /// orderItems = null;
    /// orderItems.AddIfUnique(newItem); // Does nothing and returns null silently
    /// </example>
    public static ICollection<T> AddIfUnique<T>(this ICollection<T> collection, T item, IEqualityComparer<T> comparer = null)
    {
        if (collection == null)
        {
            return null;
        }

        comparer ??= EqualityComparer<T>.Default;
        if (!collection.Contains(item, comparer))
        {
            collection.Add(item);
        }
        return collection;
    }

    /// <summary>
    /// Adds an item to the collection if all specified conditions are true.
    /// If the collection is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to add to, or null.</param>
    /// <param name="item">The item to add.</param>
    /// <param name="predicates">The predicates that must all return true. Add if empty</param>
    /// <returns>The collection (for chaining if desired), or null if the collection was null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicates"/> is null or empty and the collection is not null.</exception>
    /// <example>
    /// var orderItems = new List<Item>();
    /// var item = new Item { Price = 25.0m, Quantity = 1 };
    /// orderItems.AddIfAllConditions(item, x => x.Price > 20.0m, x => x.Quantity > 0); // Adds if both true
    /// orderItems = null;
    /// orderItems.AddIfAllConditions(item, x => x.Price > 20.0m); // Does nothing and returns null silently
    /// </example>
    public static ICollection<T> AddIfAll<T>(this ICollection<T> collection, T item, params Func<T, bool>[] predicates)
    {
        if (collection == null)
        {
            return null;
        }

        if (predicates.IsNullOrEmpty() || predicates.All(predicate => predicate(item)))
        {
            collection.Add(item);
        }
        return collection;
    }

    /// <summary>
    /// Adds an item to the collection if any specified condition is true.
    /// If the collection is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="collection">The collection to add to, or null.</param>
    /// <param name="item">The item to add.</param>
    /// <param name="predicates">The predicates, at least one of which must return true.</param>
    /// <returns>The collection (for chaining if desired), or null if the collection was null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicates"/> is null or empty and the collection is not null.</exception>
    /// <example>
    /// var orderItems = new List<Item>();
    /// var item = new Item { Price = 30.0m };
    /// orderItems.AddIfAnyCondition(item, x => x.Price > 25.0m, x => x.Quantity > 5); // Adds if either true
    /// orderItems = null;
    /// orderItems.AddIfAnyCondition(item, x => x.Price > 25.0m); // Does nothing and returns null silently
    /// </example>
    public static ICollection<T> AddIfAny<T>(this ICollection<T> collection, T item, params Func<T, bool>[] predicates)
    {
        if (collection == null)
        {
            return null;
        }

        if (predicates.SafeAny(predicate => predicate(item)))
        {
            collection.Add(item);
        }
        return collection;
    }
}