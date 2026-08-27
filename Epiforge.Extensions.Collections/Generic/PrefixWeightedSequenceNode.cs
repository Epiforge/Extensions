namespace Epiforge.Extensions.Collections.Generic;

/// <summary>
/// Represents the position of an item within a <see cref="PrefixWeightedSequence{T}"/>, and remains valid for as long as that item remains in the sequence
/// </summary>
/// <typeparam name="T">The type of the items in the sequence</typeparam>
public sealed class PrefixWeightedSequenceNode<T>
{
    internal PrefixWeightedSequenceNode(T item, int weight, uint priority)
    {
        Item = item;
        Priority = priority;
        SubtreeCount = 1;
        SubtreeWeight = weight;
        Weight = weight;
    }

    internal PrefixWeightedSequenceNode<T>? Left;
    internal PrefixWeightedSequenceNode<T>? Parent;
    internal PrefixWeightedSequenceNode<T>? Right;
    internal int SubtreeCount;
    internal int SubtreeWeight;

    /// <summary>
    /// Gets the item at this position
    /// </summary>
    public T Item { get; }

    internal uint Priority { get; }

    /// <summary>
    /// Gets the weight currently assigned to this position
    /// </summary>
    public int Weight { get; internal set; }
}
