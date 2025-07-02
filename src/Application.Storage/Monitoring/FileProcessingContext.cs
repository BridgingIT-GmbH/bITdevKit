// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the context for processing a FileEvent, passed through the processor chain.
/// </summary>
/// <remarks>
/// Initializes a new instance of the ProcessingContext with an optional FileEvent.
/// </remarks>
/// <param name="fileEvent">The FileEvent being processed, if available.</param>
public class FileProcessingContext(FileEvent fileEvent = null)
{
    /// <summary>
    /// Gets or sets the FileEvent being processed.
    /// Contains details like EventType, FilePath, and Checksum.
    /// </summary>
    public FileEvent FileEvent { get; set; } = fileEvent;

    /// <summary>
    /// Gets the dictionary of custom items stored in the context.
    /// Allows processors and behaviors to share data during processing.
    /// </summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Retrieves an item from the context by key, casting it to the specified type.
    /// Returns default(T) if the key doesn't exist or the type doesn't match.
    /// </summary>
    /// <typeparam name="T">The type to cast the item to.</typeparam>
    /// <param name="key">The key of the item to retrieve.</param>
    /// <returns>The item cast to type T, or default(T) if not found or invalid.</returns>
    public T GetItem<T>(string key)
    {
        if (this.Items.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Retrieves an item from the context by key, throwing an exception if not found or invalid.
    /// </summary>
    /// <typeparam name="T">The type to cast the item to.</typeparam>
    /// <param name="key">The key of the item to retrieve.</param>
    /// <returns>The item cast to type T.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the key doesn't exist.</exception>
    /// <exception cref="InvalidCastException">Thrown if the value can't be cast to T.</exception>
    public T GetItemOrThrow<T>(string key)
    {
        if (!this.Items.TryGetValue(key, out var value))
        {
            throw new KeyNotFoundException($"Item with key '{key}' not found in ProcessingContext.");
        }
        if (value is not T typedValue)
        {
            throw new InvalidCastException($"Item with key '{key}' cannot be cast to type '{typeof(T).Name}'.");
        }
        return typedValue;
    }

    /// <summary>
    /// Tries to retrieve an item from the context by key, casting it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the item to.</typeparam>
    /// <param name="key">The key of the item to retrieve.</param>
    /// <param name="value">The retrieved item, or default(T) if not found or invalid.</param>
    /// <returns>True if the item was found and cast successfully; false otherwise.</returns>
    public bool TryGetItem<T>(string key, out T value)
    {
        if (this.Items.TryGetValue(key, out var rawValue) && rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Sets an item in the context with the specified key and value.
    /// Overwrites any existing value for the key.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key to associate with the value.</param>
    /// <param name="value">The value to store.</param>
    public void SetItem<T>(string key, T value)
    {
        this.Items[key] = value;
    }

    /// <summary>
    /// Checks if an item exists in the context with the specified key.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key exists; false otherwise.</returns>
    public bool HasItem(string key)
    {
        return this.Items.ContainsKey(key);
    }

    /// <summary>
    /// Removes an item from the context by key.
    /// </summary>
    /// <param name="key">The key of the item to remove.</param>
    public void RemoveItem(string key)
    {
        this.Items.Remove(key);
    }
}