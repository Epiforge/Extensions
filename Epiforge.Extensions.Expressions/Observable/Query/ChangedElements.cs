namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Gathers the elements a change adds, removes or moves, holding a lone element without allocating since a lone element is what most changes carry, and states them to a notification the way it will keep them
/// </summary>
struct ChangedElements<TElement>
{
    TElement first;
    List<TElement>? all;

    public int Count { readonly get; private set; }

    public void Add(TElement element)
    {
        if (Count == 0)
            first = element;
        else
            (all ??= [first]).Add(element);
        ++Count;
    }

    /// <summary>
    /// Reverses the elements gathered, for a caller which gathers them from last to first, so that they stand in order without each being inserted at the front
    /// </summary>
    public void Reverse()
    {
        if (Count > 1)
        {
            all!.Reverse();
            first = all[0];
        }
    }

    /// <summary>
    /// Gets the elements as the list a notification which carries more than one element or carries both added and removed elements is given
    /// </summary>
    public readonly IList AsList() =>
        Count == 1 ? new TElement[] { first } : all!;

    public readonly void InsertInto(List<TElement> list, int index)
    {
        if (Count == 1)
            list.Insert(index, first);
        else
            list.InsertRange(index, all!);
    }

    public readonly NotifyCollectionChangedEventArgs ToAddition(int index) =>
        Count == 1 ? new(NotifyCollectionChangedAction.Add, (object?)first, index) : new(NotifyCollectionChangedAction.Add, all, index);

    public readonly NotifyCollectionChangedEventArgs ToMove(int index, int oldIndex) =>
        Count == 1 ? new(NotifyCollectionChangedAction.Move, (object?)first, index, oldIndex) : new(NotifyCollectionChangedAction.Move, all, index, oldIndex);

    public readonly NotifyCollectionChangedEventArgs ToRemoval(int index) =>
        Count == 1 ? new(NotifyCollectionChangedAction.Remove, (object?)first, index) : new(NotifyCollectionChangedAction.Remove, all, index);

    public readonly NotifyCollectionChangedEventArgs ToReplacementOf(in ChangedElements<TElement> replaced, int index) =>
        Count == 1 && replaced.Count == 1 ? new(NotifyCollectionChangedAction.Replace, (object?)first, (object?)replaced.first, index) : new(NotifyCollectionChangedAction.Replace, AsList(), replaced.AsList(), index);
}
