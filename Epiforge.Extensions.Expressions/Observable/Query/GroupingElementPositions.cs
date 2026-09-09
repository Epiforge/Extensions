namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Keeps where every occurrence of every element of one grouping is, so that taking an element out of it does not require searching for it
/// </summary>
/// <typeparam name="TElement">The type of the elements of the grouping</typeparam>
/// <remarks>
/// A grouping holds its elements in the order they were added and takes the first occurrence away when one is removed, which a collection can only do by looking for it. The search is the larger half of what a removal costs and its share grows with the group: measured over an <see cref="ObservableRangeCollection{T}" /> of sixty-four elements it is 41% of the linear cost, and over four thousand it is 78%. Keeping the positions instead turns the search into a walk of a tree whose depth is logarithmic in the size of the group
/// </remarks>
/// <remarks>
/// The shift which closes the gap behind a removed element is not avoided and cannot be while what the caller reads is an array. What this removes is the looking, not the moving
/// </remarks>
/// <remarks>
/// Elements which occur once — nearly all of them — are held as their node alone rather than in a list of one, because a list per distinct element would cost more than the search it saves. The occurrences of an element which repeats are kept in the order they were added, so the first of them is the one a removal takes
/// </remarks>
sealed class GroupingElementPositions<TElement>
{
    readonly NullableKeyDictionary<TElement, object> occurrencesByElement = [];
    readonly PrefixWeightedSequence<TElement> positions = new();

    /// <summary>
    /// Records that an element has been added to the end of the grouping
    /// </summary>
    internal void Append(TElement element)
    {
        var node = positions.Insert(positions.Count, element, 1);
        if (!occurrencesByElement.TryGetValue(element, out var occurrences))
            occurrencesByElement.Add(element, node);
        else if (occurrences is List<PrefixWeightedSequenceNode<TElement>> repeated)
            repeated.Add(node);
        else
            occurrencesByElement[element] = new List<PrefixWeightedSequenceNode<TElement>> { (PrefixWeightedSequenceNode<TElement>)occurrences, node };
    }

    /// <summary>
    /// Forgets every position, which is what a grouping whose contents have been replaced wholesale requires
    /// </summary>
    internal void Reset(IEnumerable<TElement> elements)
    {
        positions.Clear();
        occurrencesByElement.Clear();
        foreach (var element in elements)
            Append(element);
    }

    /// <summary>
    /// Takes the position of the first occurrence of an element and forgets it, reporting whether the element was there at all
    /// </summary>
    /// <param name="element">The element an occurrence of which is being removed</param>
    /// <param name="index">Where in the grouping that occurrence is, which is where the caller must remove it from</param>
    /// <returns><c>true</c> where the element occurred in the grouping, otherwise <c>false</c></returns>
    /// <remarks>
    /// The index is taken before anything is forgotten, so it describes the grouping as the caller still sees it
    /// </remarks>
    internal bool TryTakeFirstPosition(TElement element, out int index)
    {
        if (!occurrencesByElement.TryGetValue(element, out var occurrences))
        {
            index = -1;
            return false;
        }
        PrefixWeightedSequenceNode<TElement> node;
        if (occurrences is List<PrefixWeightedSequenceNode<TElement>> repeated)
        {
            node = repeated[0];
            repeated.RemoveAt(0);
            if (repeated.Count == 1)
                occurrencesByElement[element] = repeated[0];
        }
        else
        {
            node = (PrefixWeightedSequenceNode<TElement>)occurrences;
            occurrencesByElement.Remove(element);
        }
        index = positions.IndexOf(node);
        positions.RemoveAt(index);
        return true;
    }
}
