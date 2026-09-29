namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Compares the elements of an ordering by the keys it keeps for them, reading each key where the ordering keeps it rather than looking it up for every comparison
/// </summary>
/// <remarks>
/// A missing key sorts ahead of any other in ascending order and after any other in descending order, as the default comparer places null, and two missing keys tie, leaving the elements to the keys after them
/// </remarks>
sealed class ObservableCollectionOrderingComparer<TElement>(IReadOnlyList<bool> directions) :
    IComparer<ObservableCollectionOrderingComparer<TElement>.Entry>
{
    /// <summary>
    /// The keys an ordering keeps for one distinct element, the observations yielding them and where the element stands
    /// </summary>
    internal sealed class Entry(TElement element, int keyCount)
    {
        internal readonly TElement Element = element;
        internal int FaultedKeys;
        internal readonly Key[] Keys = new Key[keyCount];
        internal PrefixWeightedSequenceNode<Entry>? Node;
        internal int Occurrences;
    }

    internal struct Key
    {
        internal IComparable? Comparable;
        internal Exception? Fault;
        internal IObservableExpression<TElement, IComparable> Observation;
    }

    public int Compare(Entry? x, Entry? y)
    {
        var xKeys = x!.Keys;
        var yKeys = y!.Keys;
        for (int i = 0, ii = directions.Count; i < ii; ++i)
        {
            var isDescending = directions[i];
            var xComparable = xKeys[i].Comparable;
            var yComparable = yKeys[i].Comparable;
            if (xComparable is null)
            {
                if (yComparable is null)
                    continue;
                return isDescending ? 1 : -1;
            }
            if (yComparable is null)
                return isDescending ? -1 : 1;
            var comparison = xComparable.CompareTo(yComparable);
            if (comparison != 0)
                return isDescending ? comparison > 0 ? -1 : 1 : comparison;
        }
        return 0;
    }
}
