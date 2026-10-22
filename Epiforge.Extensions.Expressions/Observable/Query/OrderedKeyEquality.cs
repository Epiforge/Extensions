namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Determines the equality of keys by a comparer which orders them, for a source dictionary whose keys are equal where its comparer orders them alike; keys a comparer can only order share one hash code, so looking one up takes as long as the keys held
/// </summary>
sealed class OrderedKeyEquality<TKey>(IComparer<TKey> comparer) :
    IEqualityComparer<TKey>
{
    static readonly ConditionalWeakTable<IComparer<TKey>, OrderedKeyEquality<TKey>> byComparer = [];

    public bool Equals(TKey? x, TKey? y) =>
        comparer.Compare(x!, y!) == 0;

    /// <summary>
    /// Gets the equality of keys defined by a comparer ordering them: the comparer itself where it also determines equality, default equality for the default ordering, and otherwise equality by ordering, built once for each comparer and kept for as long as the comparer is
    /// </summary>
    internal static IEqualityComparer<TKey> For(IComparer<TKey> comparer) =>
        comparer switch
        {
            IEqualityComparer<TKey> equalityComparer => equalityComparer,
            _ when ReferenceEquals(comparer, Comparer<TKey>.Default) => EqualityComparer<TKey>.Default,
            _ => byComparer.GetValue(comparer, static source => new(source))
        };

    public int GetHashCode([DisallowNull] TKey obj) =>
        0;
}
