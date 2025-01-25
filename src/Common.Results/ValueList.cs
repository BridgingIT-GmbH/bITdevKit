// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// A memory-efficient collection optimized for very small sets of items (0-2).
/// This struct provides value semantics and avoids heap allocations for the most common scenarios
/// where only a few items need to be stored. It uses a hybrid approach:
/// - 0-2 items: Stored directly in the struct (stack/inline)
/// - 3+ items: Stored in a List{T} on the heap
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public readonly struct ValueList<T>
{
    // Direct storage for common cases (0-2 items)
    private readonly T item1;
    private readonly T item2;

    // Overflow storage for less common cases (3+ items)
    private readonly List<T> overflow;

    // Total count of items (0-255, byte is sufficient)
    private readonly byte count;

    /// <summary>
    /// Gets the number of items in the collection.
    /// </summary>
    public int Count => this.count;

    /// <summary>
    /// Gets a value indicating whether the collection is empty.
    /// This property is more efficient than checking Count == 0.
    /// </summary>
    public bool IsEmpty => this.count == 0;

    /// <summary>
    /// Creates a new ValueList with a single item.
    /// This is a zero-allocation operation as the item is stored inline.
    /// </summary>
    /// <param name="item">The item to store.</param>
    private ValueList(T item)
    {
        this.item1 = item;
        this.item2 = default;
        this.overflow = null;
        this.count = 1;
    }

    /// <summary>
    /// Creates a new ValueList with two items.
    /// This is a zero-allocation operation as both items are stored inline.
    /// </summary>
    /// <param name="item1">The first item to store.</param>
    /// <param name="item2">The second item to store.</param>
    private ValueList(T item1, T item2)
    {
        this.item1 = item1;
        this.item2 = item2;
        this.overflow = null;
        this.count = 2;
    }

    /// <summary>
    /// Creates a new ValueList with three items.
    /// This operation allocates a List{T} as we've exceeded inline storage capacity.
    /// </summary>
    /// <param name="item1">The first item.</param>
    /// <param name="item2">The second item.</param>
    /// <param name="item3">The third item.</param>
    private ValueList(T item1, T item2, T item3)
    {
        // When exceeding inline capacity, move to heap storage
        this.overflow = [item1, item2, item3];
        this.item1 = default;
        this.item2 = default;
        this.count = 3;
    }

    /// <summary>
    /// Creates a new ValueList from an existing list plus a new item.
    /// This constructor is used when adding items to an already-overflowed collection.
    /// </summary>
    /// <param name="existing">The existing list of items.</param>
    /// <param name="newItem">The new item to add.</param>
    private ValueList(List<T> existing, T newItem)
    {
        // Create a new list with space for the additional item
        this.overflow = [.. existing, newItem];
        this.item1 = default;
        this.item2 = default;
        this.count = (byte)this.overflow.Count;
    }

    /// <summary>
    /// Creates a new ValueList containing the current items plus a new item.
    /// This method maintains immutability by returning a new instance.
    /// The storage strategy (inline vs heap) is automatically handled based on item count.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>A new ValueList containing all current items plus the new item.</returns>
    public ValueList<T> Add(T item)
    {
        return this.count switch
        {
            0 => new ValueList<T>(item),                        // First item: inline storage
            1 => new ValueList<T>(this.item1, item),           // Second item: still inline
            2 => new ValueList<T>(this.item1, this.item2, item), // Third item: move to heap
            _ => new ValueList<T>(this.overflow, item)         // Already on heap: extend list
        };
    }

    /// <summary>
    /// Creates a new ValueList containing the current items plus a range of new items.
    /// This method maintains immutability by returning a new instance.
    /// </summary>
    /// <param name="items">The items to add.</param>
    /// <returns>A new ValueList containing all current items plus the new items.</returns>
    public ValueList<T> AddRange(IEnumerable<T> items)
    {
        if (items is null)
        {
            return this;
        }

        var result = this;
        foreach (var item in items)
        {
            result = result.Add(item);
        }
        return result;
    }

    /// <summary>
    /// Returns an enumerable sequence of all items in the collection.
    /// This method provides efficient enumeration based on the current storage strategy.
    /// </summary>
    /// <returns>An enumerable sequence of all items.</returns>
    public IEnumerable<T> AsEnumerable()
    {
        return this.count switch
        {
            0 => Array.Empty<T>(),                    // Empty: return cached empty array
            1 => new[] { this.item1 },                // One item: single-element array
            2 => new[] { this.item1, this.item2 },    // Two items: two-element array
            _ => this.overflow                        // Three or more: return list
        };
    }
}