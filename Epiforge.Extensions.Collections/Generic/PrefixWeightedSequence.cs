namespace Epiforge.Extensions.Collections.Generic;

/// <summary>
/// Represents a sequence of items, each carrying a non-negative weight, in which positional insertion and removal, the total weight preceding a position, and the item occupying a weight offset are all logarithmic in the number of items
/// </summary>
/// <typeparam name="T">The type of the items in the sequence</typeparam>
public sealed class PrefixWeightedSequence<T>
{
    static int CountOf(PrefixWeightedSequenceNode<T>? node) =>
        node is null ? 0 : node.SubtreeCount;

    static uint MixPriority(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x85EBCA6B;
            value ^= value >> 13;
            value *= 0xC2B2AE35;
            value ^= value >> 16;
        }
        return value;
    }

    static PrefixWeightedSequenceNode<T>? Merge(PrefixWeightedSequenceNode<T>? left, PrefixWeightedSequenceNode<T>? right)
    {
        if (left is null)
        {
            if (right is not null)
                right.Parent = null;
            return right;
        }
        if (right is null)
        {
            left.Parent = null;
            return left;
        }
        if (left.Priority > right.Priority)
        {
            left.Right = Merge(left.Right, right);
            Update(left);
            left.Parent = null;
            return left;
        }
        right.Left = Merge(left, right.Left);
        Update(right);
        right.Parent = null;
        return right;
    }

    static (PrefixWeightedSequenceNode<T>? First, PrefixWeightedSequenceNode<T>? Remainder) Split(PrefixWeightedSequenceNode<T>? node, int count)
    {
        if (node is null)
            return (null, null);
        var leftCount = CountOf(node.Left);
        if (count <= leftCount)
        {
            var (first, remainder) = Split(node.Left, count);
            node.Left = remainder;
            Update(node);
            node.Parent = null;
            if (first is not null)
                first.Parent = null;
            return (first, node);
        }
        var (takenFromRight, remainingOnRight) = Split(node.Right, count - leftCount - 1);
        node.Right = takenFromRight;
        Update(node);
        node.Parent = null;
        if (remainingOnRight is not null)
            remainingOnRight.Parent = null;
        return (node, remainingOnRight);
    }

    static void Update(PrefixWeightedSequenceNode<T> node)
    {
        node.SubtreeCount = 1 + CountOf(node.Left) + CountOf(node.Right);
        node.SubtreeWeight = node.Weight + WeightOf(node.Left) + WeightOf(node.Right);
        if (node.Left is not null)
            node.Left.Parent = node;
        if (node.Right is not null)
            node.Right.Parent = node;
    }

    static int WeightOf(PrefixWeightedSequenceNode<T>? node) =>
        node is null ? 0 : node.SubtreeWeight;

    uint priorityCounter;
    PrefixWeightedSequenceNode<T>? root;

    /// <summary>
    /// Gets the number of items in the sequence
    /// </summary>
    public int Count =>
        CountOf(root);

    /// <summary>
    /// Gets the first node in the sequence, or <c>null</c> when the sequence is empty
    /// </summary>
    public PrefixWeightedSequenceNode<T>? FirstNode
    {
        get
        {
            var leftmost = root;
            while (leftmost is not null && leftmost.Left is not null)
                leftmost = leftmost.Left;
            return leftmost;
        }
    }

    /// <summary>
    /// Gets the sum of the weights of every item in the sequence
    /// </summary>
    public int TotalWeight =>
        WeightOf(root);

    /// <summary>
    /// Removes every item from the sequence
    /// </summary>
    public void Clear() =>
        root = null;

    /// <summary>
    /// Gets the position of the specified node within the sequence
    /// </summary>
    /// <param name="node">A node belonging to this sequence</param>
    /// <returns>The zero-based position of <paramref name="node"/></returns>
    public int IndexOf(PrefixWeightedSequenceNode<T> node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var index = CountOf(node.Left);
        for (var child = node; child.Parent is not null; child = child.Parent)
            if (ReferenceEquals(child, child.Parent.Right))
                index += CountOf(child.Parent.Left) + 1;
        return index;
    }

    /// <summary>
    /// Inserts an item at the specified position
    /// </summary>
    /// <param name="index">The zero-based position at which to insert the item</param>
    /// <param name="item">The item to insert</param>
    /// <param name="weight">The weight to assign to the item</param>
    /// <returns>The node representing the item's position</returns>
    public PrefixWeightedSequenceNode<T> Insert(int index, T item, int weight)
    {
        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight));
        var inserted = new PrefixWeightedSequenceNode<T>(item, weight, MixPriority(++priorityCounter));
        var (first, remainder) = Split(root, index);
        root = Merge(Merge(first, inserted), remainder);
        return inserted;
    }

    /// <summary>
    /// Moves a range of items to a new position, which is expressed relative to the sequence with the range already removed from it
    /// </summary>
    /// <param name="oldIndex">The zero-based position of the first item to move</param>
    /// <param name="newIndex">The zero-based position at which to reinsert the items</param>
    /// <param name="count">The number of items to move</param>
    public void MoveRange(int oldIndex, int newIndex, int count)
    {
        if (count < 0 || count > Count)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (oldIndex < 0 || oldIndex > Count - count)
            throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if (newIndex < 0 || newIndex > Count - count)
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        var (before, remainder) = Split(root, oldIndex);
        var (moved, after) = Split(remainder, count);
        var (head, tail) = Split(Merge(before, after), newIndex);
        root = Merge(Merge(head, moved), tail);
    }

    /// <summary>
    /// Gets the node following the specified node, or <c>null</c> when it is the last in the sequence
    /// </summary>
    /// <param name="node">A node belonging to this sequence</param>
    /// <returns>The next node, or <c>null</c></returns>
    public PrefixWeightedSequenceNode<T>? Next(PrefixWeightedSequenceNode<T> node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Right is not null)
        {
            var leftmost = node.Right;
            while (leftmost.Left is not null)
                leftmost = leftmost.Left;
            return leftmost;
        }
        var child = node;
        var parent = node.Parent;
        while (parent is not null && ReferenceEquals(child, parent.Right))
        {
            child = parent;
            parent = parent.Parent;
        }
        return parent;
    }

    /// <summary>
    /// Gets the node at the specified position
    /// </summary>
    /// <param name="index">The zero-based position</param>
    /// <returns>The node at <paramref name="index"/></returns>
    public PrefixWeightedSequenceNode<T> NodeAt(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var current = root;
        while (current is not null)
        {
            var leftCount = CountOf(current.Left);
            if (index < leftCount)
                current = current.Left;
            else if (index == leftCount)
                return current;
            else
            {
                index -= leftCount + 1;
                current = current.Right;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    /// <summary>
    /// Gets the node which spans the specified offset into the total weight, or <c>null</c> when the offset lies beyond it
    /// </summary>
    /// <param name="weightOffset">The zero-based offset into the total weight</param>
    /// <returns>The node spanning <paramref name="weightOffset"/>, or <c>null</c></returns>
    public PrefixWeightedSequenceNode<T>? NodeAtWeight(int weightOffset)
    {
        if (weightOffset < 0)
            return null;
        var current = root;
        while (current is not null)
        {
            var leftWeight = WeightOf(current.Left);
            if (weightOffset < leftWeight)
                current = current.Left;
            else
            {
                weightOffset -= leftWeight;
                if (weightOffset < current.Weight)
                    return current;
                weightOffset -= current.Weight;
                current = current.Right;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the sum of the weights of the items preceding the specified position
    /// </summary>
    /// <param name="index">The zero-based position</param>
    /// <returns>The total weight of the items before <paramref name="index"/></returns>
    public int PrefixWeightBefore(int index)
    {
        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var prefixWeight = 0;
        var current = root;
        while (current is not null)
        {
            var leftCount = CountOf(current.Left);
            if (index <= leftCount)
                current = current.Left;
            else
            {
                prefixWeight += WeightOf(current.Left) + current.Weight;
                index -= leftCount + 1;
                current = current.Right;
            }
        }
        return prefixWeight;
    }

    /// <summary>
    /// Removes the item at the specified position
    /// </summary>
    /// <param name="index">The zero-based position of the item to remove</param>
    /// <returns>The node which represented the removed item</returns>
    public PrefixWeightedSequenceNode<T> RemoveAt(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var (before, remainder) = Split(root, index);
        var (removed, after) = Split(remainder, 1);
        root = Merge(before, after);
        removed!.Left = null;
        removed.Parent = null;
        removed.Right = null;
        removed.SubtreeCount = 1;
        removed.SubtreeWeight = removed.Weight;
        return removed;
    }

    /// <summary>
    /// Assigns a new weight to the specified node
    /// </summary>
    /// <param name="node">A node belonging to this sequence</param>
    /// <param name="weight">The weight to assign</param>
    public void SetWeight(PrefixWeightedSequenceNode<T> node, int weight)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight));
        if (node.Weight == weight)
            return;
        node.Weight = weight;
        for (var ancestor = node; ancestor is not null; ancestor = ancestor.Parent)
            ancestor.SubtreeWeight = ancestor.Weight + WeightOf(ancestor.Left) + WeightOf(ancestor.Right);
    }
}
