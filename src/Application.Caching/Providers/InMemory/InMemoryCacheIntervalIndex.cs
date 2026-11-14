// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

// A lower-bound AVL tree stores subtree maximum upper bounds. Queries prune disjoint subtrees.
// Callers serialize writes/reads through their hash shard; this tree contains no global lock.

/// <summary>Indexes half-open intervals in an AVL tree with subtree pruning and caller-owned synchronization.</summary>
/// <example><code>index.Find(10, 20, null, null, candidates, 128);</code></example>
public sealed class InMemoryCacheIntervalIndex
{
    private Node root;

    /// <summary>Gets the number of indexed immutable intervals.</summary>
    public int Count { get; private set; }

    /// <summary>Adds one interval and rebalances the tree; duplicate immutable interval keys are rejected.</summary>
    public void Add(long lower, long upper, InMemoryCacheEntry entry)
    {
        this.root = Insert(this.root, new(lower, upper, entry));
        this.Count++;
    }

    /// <summary>Removes a known interval and rebalances affected ancestors.</summary>
    public void Remove(long lower, InMemoryCacheEntry entry)
    {
        this.root = Delete(this.root, lower, entry.SortKey);
        this.Count--;
    }

    /// <summary>Appends overlapping intervals after the supplied lower-bound/key cursor up to the finite output limit.</summary>
    public void Find(long lower, long upper, long? afterLower, string afterKey,
        List<InMemoryCacheIndexCandidate> output, int maximum) =>
        Visit(this.root, lower, upper, afterLower, afterKey, output, maximum);

    private static Node Insert(Node root, Node added)
    {
        if (root is null)
        {
            return added;
        }

        var comparison = Compare(added.Lower, added.Entry.SortKey, root.Lower, root.Entry.SortKey);
        if (comparison < 0)
        {
            root.Left = Insert(root.Left, added);
        }
        else if (comparison > 0)
        {
            root.Right = Insert(root.Right, added);
        }
        else
        {
            throw new InvalidOperationException("An immutable interval version was indexed twice.");
        }

        return Balance(root);
    }

    private static Node Delete(Node root, long lower, string key)
    {
        if (root is null)
        {
            return null;
        }

        var comparison = Compare(lower, key, root.Lower, root.Entry.SortKey);
        if (comparison < 0)
        {
            root.Left = Delete(root.Left, lower, key);
        }
        else if (comparison > 0)
        {
            root.Right = Delete(root.Right, lower, key);
        }
        else if (root.Left is null || root.Right is null)
        {
            return root.Left ?? root.Right;
        }
        else
        {
            var next = root.Right;
            while (next.Left is not null)
            {
                next = next.Left;
            }

            root.Lower = next.Lower;
            root.Upper = next.Upper;
            root.Entry = next.Entry;
            root.Right = Delete(root.Right, next.Lower, next.Entry.SortKey);
        }

        return Balance(root);
    }

    private static void Visit(Node node, long lower, long upper, long? afterLower, string afterKey,
        List<InMemoryCacheIndexCandidate> output, int maximum)
    {
        if (node is null || output.Count >= maximum || node.MaximumUpper <= lower)
        {
            return;
        }

        var after = !afterLower.HasValue || Compare(node.Lower, node.Entry.SortKey, afterLower.Value, afterKey) > 0;
        if (after)
        {
            Visit(node.Left, lower, upper, afterLower, afterKey, output, maximum);
        }

        if (output.Count >= maximum)
        {
            return;
        }

        if (after && node.Lower < upper && node.Upper > lower)
        {
            output.Add(new(node.Entry, node.Lower));
        }

        if (node.Lower < upper)
        {
            Visit(node.Right, lower, upper, afterLower, afterKey, output, maximum);
        }
    }

    private static Node Balance(Node node)
    {
        Refresh(node);
        var difference = Height(node.Left) - Height(node.Right);
        if (difference > 1)
        {
            if (Height(node.Left.Left) < Height(node.Left.Right))
            {
                node.Left = RotateLeft(node.Left);
            }

            return RotateRight(node);
        }

        if (difference < -1)
        {
            if (Height(node.Right.Right) < Height(node.Right.Left))
            {
                node.Right = RotateRight(node.Right);
            }

            return RotateLeft(node);
        }

        return node;
    }

    private static Node RotateLeft(Node node)
    {
        var next = node.Right;
        node.Right = next.Left;
        next.Left = node;
        Refresh(node);
        Refresh(next);
        return next;
    }

    private static Node RotateRight(Node node)
    {
        var next = node.Left;
        node.Left = next.Right;
        next.Right = node;
        Refresh(node);
        Refresh(next);
        return next;
    }

    private static void Refresh(Node node)
    {
        node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));
        node.MaximumUpper = Math.Max(node.Upper, Math.Max(node.Left?.MaximumUpper ?? long.MinValue, node.Right?.MaximumUpper ?? long.MinValue));
    }

    private static int Height(Node node) => node?.Height ?? 0;

    private static int Compare(long lower, string key, long otherLower, string otherKey) =>
        lower == otherLower ? StringComparer.Ordinal.Compare(key, otherKey) : lower.CompareTo(otherLower);

    private sealed class Node(long lower, long upper, InMemoryCacheEntry entry)
    {
        /// <summary>Stores the inclusive interval lower bound.</summary>
        public long Lower = lower;

        /// <summary>Stores the exclusive interval upper bound.</summary>
        public long Upper = upper;

        /// <summary>References the immutable generation indexed by this node.</summary>
        public InMemoryCacheEntry Entry = entry;

        /// <summary>References intervals ordered before this node.</summary>
        public Node Left;

        /// <summary>References intervals ordered after this node.</summary>
        public Node Right;

        /// <summary>Stores the AVL subtree height used by rebalancing.</summary>
        public int Height = 1;

        /// <summary>Stores the largest subtree upper bound used to prune disjoint queries.</summary>
        public long MaximumUpper = upper;
    }
}

/// <summary>References an immutable entry and its numeric ordering value for bounded continuation.</summary>
/// <param name="Entry">The immutable candidate generation.</param>
/// <param name="OrderValue">The numeric ordering value used with the composite key.</param>
/// <example><code>var metadata = candidate.Entry.Metadata;</code></example>
public readonly record struct InMemoryCacheIndexCandidate(InMemoryCacheEntry Entry, long OrderValue = 0);