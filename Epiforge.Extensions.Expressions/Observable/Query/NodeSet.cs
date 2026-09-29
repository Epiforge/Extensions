namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Holds the positions at which one thing occurs in a sequence in a single field, the lone position itself until there is a second and a list of them after that, since nearly everything occurs once, a list of one costs 64 bytes, and a dictionary's entry is no larger for it than it was for the list alone
/// </summary>
/// <remarks>
/// It is a value kept in a dictionary, so a change to it must be made through a reference to the dictionary's own copy
/// </remarks>
struct NodeSet<T>
{
    internal NodeSet(PrefixWeightedSequenceNode<T> first) =>
        nodes = first;

    object? nodes;

    internal readonly int Count =>
        nodes switch
        {
            null => 0,
            PrefixWeightedSequenceNode<T> => 1,
            _ => Unsafe.As<List<PrefixWeightedSequenceNode<T>>>(nodes).Count
        };

    internal readonly PrefixWeightedSequenceNode<T> this[int index] =>
        nodes as PrefixWeightedSequenceNode<T> ?? Unsafe.As<List<PrefixWeightedSequenceNode<T>>>(nodes!)[index];

    internal void Add(PrefixWeightedSequenceNode<T> node)
    {
        if (nodes is null)
            nodes = node;
        else if (nodes is PrefixWeightedSequenceNode<T> first)
            nodes = new List<PrefixWeightedSequenceNode<T>> { first, node };
        else
            Unsafe.As<List<PrefixWeightedSequenceNode<T>>>(nodes).Add(node);
    }

    internal void Remove(PrefixWeightedSequenceNode<T> node)
    {
        if (ReferenceEquals(nodes, node))
            nodes = null;
        else if (nodes is not PrefixWeightedSequenceNode<T> and not null)
            Unsafe.As<List<PrefixWeightedSequenceNode<T>>>(nodes).Remove(node);
    }
}
