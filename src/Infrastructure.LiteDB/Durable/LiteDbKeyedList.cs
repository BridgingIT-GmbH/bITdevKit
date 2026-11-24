// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Collections;

/// <summary>Persists appendable records individually while exposing bounded command-local sequence operations.</summary>
/// <typeparam name="T">The stored record type.</typeparam>
/// <param name="records">The individual native records.</param>
/// <param name="identity">The stable identity selector.</param>
public sealed class LiteDbKeyedList<T>(LiteDbKeyedCollection<Guid, T> records, Func<T, Guid> identity) : IEnumerable<T>
{
    /// <summary>Gets the bounded number of records.</summary>
    public int Count => records.Count;

    /// <summary>Appends one record using its stable identity.</summary>
    /// <param name="value">The record to append.</param>
    public void Add(T value) => records.Add(identity(value), value);

    /// <summary>Removes a bounded selection of records atomically with its owning command.</summary>
    /// <param name="predicate">The selection predicate.</param>
    /// <returns>The number removed.</returns>
    public int RemoveAll(Predicate<T> predicate)
    {
        var targets = records.Values.Where(item => predicate(item)).ToArray();
        foreach (var item in targets) { records.Remove(identity(item)); }

        return targets.Length;
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => records.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
