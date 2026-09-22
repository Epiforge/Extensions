namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Continuously finds the greatest or least value in a collection according to a comparer
/// </summary>
/// <remarks>
/// The distinct values are kept in a set ordered by the comparer, each with the number of times it occurs, so that when the value held leaves the next one is read from the set rather than found by comparing every element again. An eviction costs a logarithmic number of comparisons where it had cost one for each element, at the price of the same logarithmic number for every other change, which had cost none, and for every element when the query is built.
/// The set merges values the comparer cannot tell apart even when they are not equal, as two elements with one key are when the values are elements paired with keys. An entry therefore reports one of them and keeps the others, and promotes one of the others when the one it reports leaves, so that the query never reports a value the collection no longer holds
/// </remarks>
sealed class ObservableCollectionComparisonQuery<TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TResult> observableCollectionQuery, int soughtComparison, IComparer<TResult> comparer) :
    ObservableScalarQuery<TResult>(collectionObserver)
{
    /// <summary>
    /// A value the comparer distinguishes, the number of times it occurs, and the occurrences of any other values the comparer cannot tell from it
    /// </summary>
    sealed class Occurrences(TResult value)
    {
        public int Count = 1;
        public NullableKeyDictionary<TResult, int>? Indistinguishable;
        public TResult Value = value;
    }

    sealed class OccurrencesComparer(IComparer<TResult> comparer) :
        IComparer<Occurrences>
    {
        public int Compare(Occurrences? x, Occurrences? y) =>
            comparer.Compare(x!.Value, y!.Value);
    }

#if IS_NET_9_0_OR_GREATER
    readonly Lock access = new();
#else
    readonly object access = new();
#endif
    SortedSet<Occurrences>? occurrences;
    readonly Occurrences probe = new(default!);

    internal readonly IComparer<TResult> Comparer = comparer;
    internal readonly int SoughtComparison = soughtComparison;

    void AddWithAccess(SortedSet<Occurrences> set, TResult value)
    {
        probe.Value = value;
        if (set.TryGetValue(probe, out var found))
        {
            if (EqualityComparer<TResult>.Default.Equals(found.Value, value))
                ++found.Count;
            else
            {
                var indistinguishable = found.Indistinguishable ??= [];
                indistinguishable[value] = indistinguishable.TryGetValue(value, out var count) ? count + 1 : 1;
            }
        }
        else
            set.Add(new Occurrences(value));
        probe.Value = default!;
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                observableCollectionQuery.CollectionChanged -= ObservableCollectionQueryCollectionChanged;
                observableCollectionQuery.PropertyChanged -= ObservableCollectionQueryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate()
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (observableCollectionQuery.OperationFault is { } queryFault)
            {
                occurrences = null;
                Evaluation = (queryFault, default!);
                return;
            }
            var set = new SortedSet<Occurrences>(new OccurrencesComparer(Comparer));
            if (observableCollectionQuery.HasIndexerPenalty)
                foreach (var value in observableCollectionQuery)
                    AddWithAccess(set, value);
            else
                for (int i = 0, ii = observableCollectionQuery.Count; i < ii; ++i)
                    AddWithAccess(set, observableCollectionQuery[i]);
            occurrences = set;
            ReportExtremeWithAccess(set);
        }
    }

    void ObservableCollectionQueryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Move)
            return;
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (observableCollectionQuery.OperationFault is not null)
                return;
            if (e.Action is NotifyCollectionChangedAction.Reset || occurrences is not { } set)
            {
                Evaluate();
                return;
            }
            if (e.OldItems is { } oldItems)
                for (int i = 0, ii = oldItems.Count; i < ii; ++i)
                    RemoveWithAccess(set, (TResult)oldItems[i]!);
            if (e.NewItems is { } newItems)
                for (int i = 0, ii = newItems.Count; i < ii; ++i)
                    AddWithAccess(set, (TResult)newItems[i]!);
            ReportExtremeWithAccess(set);
        }
    }

    void ObservableCollectionQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ObservableCollectionQuery<>.OperationFault))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        Evaluate();
        observableCollectionQuery.CollectionChanged += ObservableCollectionQueryCollectionChanged;
        observableCollectionQuery.PropertyChanged += ObservableCollectionQueryPropertyChanged;
    }

    void RemoveWithAccess(SortedSet<Occurrences> set, TResult value)
    {
        probe.Value = value;
        if (set.TryGetValue(probe, out var found))
        {
            if (EqualityComparer<TResult>.Default.Equals(found.Value, value))
            {
                if (--found.Count == 0)
                {
                    if (found.Indistinguishable is { Count: > 0 } promotable)
                    {
                        foreach (var (promoted, promotedCount) in promotable)
                        {
                            found.Value = promoted;
                            found.Count = promotedCount;
                            break;
                        }
                        promotable.Remove(found.Value);
                    }
                    else
                        set.Remove(found);
                }
            }
            else if (found.Indistinguishable is { } others && others.TryGetValue(value, out var remaining))
            {
                if (remaining == 1)
                    others.Remove(value);
                else
                    others[value] = remaining - 1;
            }
        }
        probe.Value = default!;
    }

    void ReportExtremeWithAccess(SortedSet<Occurrences> set) =>
        Evaluation = set.Count == 0 ? (ExceptionHelper.SequenceContainsNoElements, default!) : (null, (SoughtComparison > 0 ? set.Max : set.Min)!.Value);

    public override string ToString() =>
        $"{(SoughtComparison > 0 ? "max" : "min")} of {observableCollectionQuery}";
}
