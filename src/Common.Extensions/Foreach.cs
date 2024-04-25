// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public static partial class Extensions
{
    /// <summary>
    /// Performs an action on each value of the enumerable.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">The items.</param>
    /// <param name="action">Action to perform on every item.</param>
    /// <returns>the source with the actions applied.</returns>
    [DebuggerStepThrough]
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action, CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            action(item);
        }

        return source;
    }

    public static async Task<IEnumerable<T>> ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> action, CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(item).ConfigureAwait(false);
        }

        return source;
    }

    [DebuggerStepThrough]
    public static ICollection<T> ForEach<T>(this ICollection<T> source, Action<T> action, CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        return source.AsEnumerable().ForEach(action, cancellationToken).ToList();
    }

    public static async Task<ICollection<T>> ForEachAsync<T>(this ICollection<T> source, Func<T, Task> action, CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(item).ConfigureAwait(false);
        }

        return source;
    }

    [DebuggerStepThrough]
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> childSelector, Action<T> action, CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            action(item);
            childSelector?.Invoke(item).ForEach(childSelector, action, cancellationToken);
        }

        return source;
    }
}
