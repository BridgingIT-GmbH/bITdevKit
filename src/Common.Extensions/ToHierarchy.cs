// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Extensions
{
    /// <summary>
    ///     Creates a hierarchical structure from a flat collection.
    /// </summary>
    /// <example>
    /// <code>
    /// var items = new[] {
    ///     new { Id = 1, ParentId = (int?)null, Name = "Root" },
    ///     new { Id = 2, ParentId = 1, Name = "Child1" },
    ///     new { Id = 3, ParentId = 1, Name = "Child2" },
    ///     new { Id = 4, ParentId = 2, Name = "Grandchild" }
    /// };
    ///
    /// var tree = items.ToHierarchy(
    ///     i => i.Id,
    ///     i => i.ParentId
    /// );
    /// // Creates a tree structure:
    /// // Root
    /// // ├── Child1
    /// // │   └── Grandchild
    /// // └── Child2
    /// </code>
    /// </example>
    public static IEnumerable<HierarchyNode<T>> ToHierarchy<T, TKey>(
        this IEnumerable<T> source,
        Func<T, TKey> idSelector,
        Func<T, TKey?> parentIdSelector)
        where TKey : struct
    {
        if (source.IsNullOrEmpty() || idSelector == null || parentIdSelector == null)
        {
            return [];
        }

        var lookup = source.ToLookup(parentIdSelector, t => t);

        return CreateHierarchy(lookup, idSelector, default(TKey?));
    }

    /// <summary>
    ///     Flattens a hierarchical structure into a flat collection.
    /// </summary>
    /// <example>
    /// <code>
    /// var tree = new HierarchyNode&lt;Item&gt; {
    ///     Item = new Item { Id = 1, Name = "Root" },
    ///     Children = new[] {
    ///         new HierarchyNode&lt;Item&gt; {
    ///             Item = new Item { Id = 2, Name = "Child1" },
    ///             Children = new[] {
    ///                 new HierarchyNode&lt;Item&gt; {
    ///                     Item = new Item { Id = 4, Name = "Grandchild" }
    ///                 }
    ///             }
    ///         },
    ///         new HierarchyNode&lt;Item&gt; {
    ///             Item = new Item { Id = 3, Name = "Child2" }
    ///         }
    ///     }
    /// };
    ///
    /// var flatItems = tree.ToFlatten(
    ///     node => node.Item.Id,
    ///     (item, parentId) => item with { ParentId = parentId }
    /// );
    /// </code>
    /// </example>
    public static IEnumerable<TResult> ToFlatten<T, TKey, TResult>(
        this HierarchyNode<T> root,
        Func<T, TKey> idSelector,
        Func<T, TKey?, TResult> resultSelector)
        where TKey : struct
    {
        return FlattenHierarchy(root, idSelector, resultSelector, default);
    }

    /// <summary>
    ///     Flattens a collection of hierarchical nodes into a flat collection.
    /// </summary>
    public static IEnumerable<TResult> ToFlatten<T, TKey, TResult>(
        this IEnumerable<HierarchyNode<T>> roots,
        Func<T, TKey> idSelector,
        Func<T, TKey?, TResult> resultSelector)
        where TKey : struct
    {
        if (roots.IsNullOrEmpty())
        {
            return [];
        }

        return roots.SelectMany(root => FlattenHierarchy(root, idSelector, resultSelector, default));
    }

    private static IEnumerable<TResult> FlattenHierarchy<T, TKey, TResult>(
        HierarchyNode<T> node,
        Func<T, TKey> idSelector,
        Func<T, TKey?, TResult> resultSelector,
        TKey? parentId)
        where TKey : struct
    {
        yield return resultSelector(node.Item, parentId);

        var currentId = idSelector(node.Item);

        foreach (var child in node.Children ?? [])
        {
            foreach (var descendant in FlattenHierarchy(child, idSelector, resultSelector, currentId))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<HierarchyNode<T>> CreateHierarchy<T, TKey>(
        ILookup<TKey?, T> lookup,
        Func<T, TKey> idSelector,
        TKey? parentId)
        where TKey : struct
    {
        foreach (var item in lookup[parentId])
        {
            var id = idSelector(item);

            yield return new HierarchyNode<T>
            {
                Item = item,
                Children = CreateHierarchy(lookup, idSelector, id)
            };
        }
    }
}

public class HierarchyNode<T>
{
    public T Item { get; set; }

    public IEnumerable<HierarchyNode<T>> Children { get; set; } = [];
}