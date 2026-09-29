namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Pairs an element with its key for a grouping or a lookup, comparing both with their types' default equality, where a <see cref="Tuple{T1, T2}" /> compares them as objects and so boxes a key which is a value type on both sides of every comparison
/// </summary>
sealed class KeyedElement<TElement, TKey>(TElement item1, TKey item2) :
    IEquatable<KeyedElement<TElement, TKey>>
{
    public TElement Item1 { get; } = item1;

    public TKey Item2 { get; } = item2;

    public void Deconstruct(out TElement item1, out TKey item2)
    {
        item1 = Item1;
        item2 = Item2;
    }

    public bool Equals(KeyedElement<TElement, TKey>? other) =>
        other is not null && EqualityComparer<TElement>.Default.Equals(Item1, other.Item1) && EqualityComparer<TKey>.Default.Equals(Item2, other.Item2);

    public override bool Equals(object? obj) =>
        Equals(obj as KeyedElement<TElement, TKey>);

    public override int GetHashCode() =>
        HashCode.Combine(Item1, Item2);
}
