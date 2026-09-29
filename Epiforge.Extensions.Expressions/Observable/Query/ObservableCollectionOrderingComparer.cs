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
    /// <remarks>
    /// The first key is held in the entry itself, and only an ordering by more than one key makes entries with an array of the keys after it, since most orderings have one key and an array of one costs a header and a reference beside the key it holds
    /// </remarks>
    internal class Entry(TElement element)
    {
        internal readonly TElement Element = element;
        internal int FaultedKeys;
        internal Key FirstKey;
        internal PrefixWeightedSequenceNode<Entry>? Node;
        internal int Occurrences;

        internal static Entry Create(TElement element, int keyCount) =>
            keyCount > 1 ? new EntryWithLaterKeys(element, keyCount - 1) : new Entry(element);

        internal ref Key KeyAt(int index)
        {
            if (index == 0)
                return ref FirstKey;
            return ref ((EntryWithLaterKeys)this).LaterKeys[index - 1];
        }
    }

    /// <summary>
    /// An entry of an ordering by more than one key, holding the keys after the first
    /// </summary>
    internal sealed class EntryWithLaterKeys(TElement element, int laterKeyCount) :
        Entry(element)
    {
        internal readonly Key[] LaterKeys = new Key[laterKeyCount];
    }

    internal struct Key
    {
        internal IComparable? Comparable;
        internal Exception? Fault;
        internal IObservableExpression<TElement, IComparable> Observation;
    }

    public int Compare(Entry? x, Entry? y)
    {
        for (int i = 0, ii = directions.Count; i < ii; ++i)
        {
            var isDescending = directions[i];
            var xComparable = x!.KeyAt(i).Comparable;
            var yComparable = y!.KeyAt(i).Comparable;
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
