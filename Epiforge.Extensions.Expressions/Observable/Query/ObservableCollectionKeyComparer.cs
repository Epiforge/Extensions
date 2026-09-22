namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Compares elements paired with their keys by their keys alone, so that the comparison query can seek the element with the greatest or least key
/// </summary>
/// <remarks>
/// Two of these are equal when they wrap the same key comparer, which is what lets two observations of the same key selector and comparer share one comparison query
/// </remarks>
sealed class ObservableCollectionKeyComparer<TElement, TKey>(IComparer<TKey> keyComparer) :
    IComparer<(TElement, TKey)>
{
    internal static readonly Expression<Func<(TElement, TKey), TElement>> ElementOf = keyed => keyed.Item1;

    readonly IComparer<TKey> keyComparer = keyComparer;

    public int Compare((TElement, TKey) x, (TElement, TKey) y) =>
        keyComparer.Compare(x.Item2, y.Item2);

    public override bool Equals(object? obj) =>
        obj is ObservableCollectionKeyComparer<TElement, TKey> other && ReferenceEquals(keyComparer, other.keyComparer);

    public override int GetHashCode() =>
        RuntimeHelpers.GetHashCode(keyComparer);
}
