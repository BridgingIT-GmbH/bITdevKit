// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.ObjectModel;

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

    // Overflow storage supplies its own count.
    private readonly ReadOnlyCollection<T> overflow;

    private readonly byte inlineCount;

    /// <summary>
    /// Gets the number of items in the collection.
    /// </summary>
    public int Count => this.overflow?.Count ?? this.inlineCount;

    /// <summary>
    /// Gets a value indicating whether the collection is empty.
    /// This property is more efficient than checking Count == 0.
    /// </summary>
    public bool IsEmpty => this.overflow is null && this.inlineCount == 0;

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
        this.inlineCount = 1;
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
        this.inlineCount = 2;
    }

    /// <summary>
    /// Takes ownership of a list that has exceeded inline storage capacity.
    /// </summary>
    /// <param name="items">A list owned by the new collection.</param>
    private ValueList(List<T> items)
    {
        this.overflow = items.AsReadOnly();
        this.item1 = default;
        this.item2 = default;
        this.inlineCount = 0;
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
        if (this.overflow is not null)
        {
            return new ValueList<T>([.. this.overflow, item]);
        }

        return this.inlineCount switch
        {
            0 => new ValueList<T>(item),
            1 => new ValueList<T>(this.item1, item),
            _ => new ValueList<T>([this.item1, this.item2, item])
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

        var inline = this;
        List<T> buffer = null;

        foreach (var item in items)
        {
            if (buffer is null && inline.Count < 2)
            {
                inline = inline.Add(item);
                continue;
            }

            buffer ??= inline.overflow is null ? [inline.item1, inline.item2] : [.. inline.overflow];
            buffer.Add(item);
        }

        return buffer is null ? inline : new ValueList<T>(buffer);
    }

    /// <summary>
    /// Returns an enumerable sequence of all items in the collection.
    /// This method provides efficient enumeration based on the current storage strategy.
    /// </summary>
    /// <returns>An enumerable sequence of all items.</returns>
    public IEnumerable<T> AsEnumerable()
    {
        if (this.overflow is not null)
        {
            return this.overflow;
        }

        return this.inlineCount switch
        {
            0 => [],
            1 => [this.item1],
            _ => [this.item1, this.item2]
        };
    }
}
