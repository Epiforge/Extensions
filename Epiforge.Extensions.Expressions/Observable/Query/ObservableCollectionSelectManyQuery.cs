namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSelectManyQuery<TElement, TResult> :
    ObservableCollectionQuery<TResult>
{
    public ObservableCollectionSelectManyQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Expression<Func<TElement, IEnumerable<TResult>>> selector) :
        base(collectionObserver)
    {
        this.source = source;
        Selector = selector;
        access = new object();
        enumerableInstances = new Dictionary<IEnumerable<TResult>, int>();
    }

    readonly object access;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<IEnumerable<TResult>>? select;
    int count;
    readonly Dictionary<IEnumerable<TResult>, int> enumerableInstances;
    readonly ObservableCollectionQuery<TElement> source;

    internal readonly Expression<Func<TElement, IEnumerable<TResult>>> Selector;

    public override TResult this[int index]
    {
        get
        {
            lock (access)
            {
                for (int i = 0, ii = select!.Count; i < ii; ++i)
                {
                    var enumerable = select[i];
                    var enumerableCount = enumerable.Count();
                    if (index < enumerableCount)
                        return enumerable.ElementAt(index);
                    index -= enumerableCount;
                }
                throw new IndexOutOfRangeException();
            }
        }
    }

    public override int Count =>
        count;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is IEnumerable<TResult> enumerable)
            lock (access)
            {
                if (enumerableInstances.TryGetValue(enumerable, out var instances))
                {
                    if (e.Action is NotifyCollectionChangedAction.Reset)
                        OnCollectionChanged(e);
                    else
                    {
                        var newCount = count + ((e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0)) * instances;
                        var reducedCount = enumerable.Count();
                        var reducedIndex = 0;
                        for (int i = 0, ii = select!.Count; i < ii && instances > 0; ++i)
                        {
                            var selectEnumerable = select[i];
                            if (ReferenceEquals(selectEnumerable, enumerable))
                            {
                                --instances;
                                OnCollectionChanged(e.Action switch
                                {
                                    NotifyCollectionChangedAction.Add => new NotifyCollectionChangedEventArgs(e.Action, e.NewItems, reducedIndex + e.NewStartingIndex),
                                    NotifyCollectionChangedAction.Move => new NotifyCollectionChangedEventArgs(e.Action, e.NewItems ?? e.OldItems, reducedIndex + e.NewStartingIndex, reducedIndex + e.OldStartingIndex),
                                    NotifyCollectionChangedAction.Remove => new NotifyCollectionChangedEventArgs(e.Action, e.OldItems, reducedIndex + e.OldStartingIndex),
                                    NotifyCollectionChangedAction.Replace => new NotifyCollectionChangedEventArgs(e.Action, e.NewItems!, e.OldItems!, reducedIndex + e.OldStartingIndex),
                                    _ => throw new NotSupportedException()
                                });
                                reducedIndex += reducedCount;
                            }
                            else
                                reducedIndex += selectEnumerable.Count();
                        }
                        SetCount(newCount);
                    }
                }
            }
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                lock (access)
                {
                    foreach (var enumerable in enumerableInstances.Keys)
                        if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
                            collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
                    select!.CollectionChanged -= SelectCollectionChanged;
                    select.PropertyChanged -= SelectPropertyChanged;
                    select.PropertyChanging -= SelectPropertyChanging;
                    select.Dispose();
                    RemovedFromCache();
                }
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TResult> GetEnumerator()
    {
        lock (access)
            foreach (var enumerable in select!)
                if (enumerable is not null)
                    foreach (var element in enumerable)
                        yield return element;
    }

    int GetReducedStartingIndex(int mapIndex)
    {
        if (mapIndex < 0 || mapIndex >= select!.Count)
            return -1;
        var reducedIndex = 0;
        for (int i = 0, ii = select.Count; i < ii; ++i)
        {
            if (i == mapIndex)
                return reducedIndex;
            reducedIndex += select[i].Count();
        }
        return -1;
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            select = source.ObserveSelect(Selector);
            OperationFault = select.OperationFault;
            for (int i = 0, ii = select.Count; i < ii; ++i)
            {
                var enumerable = select[i];
                if (enumerable is not null)
                {
                    count += enumerable.Count();
                    if (enumerableInstances.TryGetValue(enumerable, out var instancesOfEnumerable))
                        enumerableInstances[enumerable] = instancesOfEnumerable + 1;
                    else
                    {
                        enumerableInstances.Add(enumerable, 1);
                        if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
                            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
                    }
                }
            }
            select.CollectionChanged += SelectCollectionChanged;
            select.PropertyChanged += SelectPropertyChanged;
            select.PropertyChanging += SelectPropertyChanging;
        }
        OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
    }

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    void SelectCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            NotifyCollectionChangedEventArgs? eventArgs = null;
            var newCount = 0;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    {
                        var reducedNewStartingIndex = 0;
                        var newItems = new List<TResult>();
                        if (e.NewItems is not null && e.NewStartingIndex >= 0)
                        {
                            reducedNewStartingIndex = GetReducedStartingIndex(e.NewStartingIndex);
                            for (int i = 0, ii = e.NewItems.Count; i < ii; ++i)
                                if (e.NewItems[i] is IEnumerable<TResult> newEnumerable)
                                {
                                    newItems.AddRange(newEnumerable);
                                    if (enumerableInstances!.TryGetValue(newEnumerable, out var instancesOfEnumerable))
                                        enumerableInstances[newEnumerable] = instancesOfEnumerable + 1;
                                    else
                                    {
                                        enumerableInstances.Add(newEnumerable, 1);
                                        if (newEnumerable is INotifyCollectionChanged collectionChangedNotifier)
                                            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
                                    }
                                }
                        }
                        var reducedOldStartingIndex = 0;
                        var oldItems = new List<TResult>();
                        if (e.OldItems is not null && e.OldStartingIndex >= 0)
                        {
                            reducedOldStartingIndex = GetReducedStartingIndex(e.OldStartingIndex);
                            if (e.OldStartingIndex > e.NewStartingIndex)
                                reducedOldStartingIndex += newItems.Count;
                            for (int i = 0, ii = e.OldItems.Count; i < ii; ++i)
                                if (e.OldItems[i] is IEnumerable<TResult> oldEnumerable)
                                {
                                    oldItems.AddRange(oldEnumerable);
                                    var instancesOfEnumerable = enumerableInstances![oldEnumerable];
                                    if (instancesOfEnumerable == 1)
                                    {
                                        enumerableInstances.Remove(oldEnumerable);
                                        if (oldEnumerable is INotifyCollectionChanged collectionChangedNotifier)
                                            collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
                                    }
                                    else
                                        enumerableInstances[oldEnumerable] = instancesOfEnumerable - 1;
                                }
                        }
                        if (oldItems.Count > 0)
                        {
                            if (newItems.Count > 0)
                                eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItems.AsReadOnly(), oldItems.AsReadOnly(), reducedOldStartingIndex);
                            else
                                eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems.AsReadOnly(), reducedOldStartingIndex);
                        }
                        else if (newItems.Count > 0)
                            eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems.AsReadOnly(), reducedNewStartingIndex);
                        newCount = count + newItems.Count - oldItems.Count;
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems!.Count > 0 && e.OldStartingIndex == e.NewStartingIndex)
                    {
                        var reducedNewStartingIndex = GetReducedStartingIndex(e.NewStartingIndex);
                        var reducedOldStartingIndex = GetReducedStartingIndex(e.OldStartingIndex);
                        var movedItems = e.OldItems.Cast<IEnumerable<TResult>>().SelectMany(enumerable => enumerable ?? Enumerable.Empty<TResult>()).ToList().AsReadOnly();
                        if (e.OldStartingIndex > e.NewStartingIndex)
                            reducedOldStartingIndex += movedItems.Count;
                        eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedItems, reducedNewStartingIndex, reducedOldStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var enumerable in enumerableInstances.Keys)
                    {
                        if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
                            collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
                    }
                    enumerableInstances.Clear();
                    for (int i = 0, ii = select!.Count; i < ii; ++i)
                    {
                        var enumerable = select[i];
                        if (enumerable is not null)
                        {
                            newCount += enumerable.Count();
                            if (enumerableInstances.TryGetValue(enumerable, out var instancesOfEnumerable))
                                enumerableInstances[enumerable] = instancesOfEnumerable + 1;
                            else
                            {
                                enumerableInstances.Add(enumerable, 1);
                                if (enumerable is INotifyCollectionChanged collectionChangedNotifier)
                                    collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
                            }
                        }
                    }
                    eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                    break;
            }
            if (eventArgs is not null)
            {
                if (eventArgs.Action is not NotifyCollectionChangedAction.Move)
                    SetCount(newCount);
                OnCollectionChanged(eventArgs);
            }
        }
    }

    void SelectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            OperationFault = select!.OperationFault;
            OnPropertyChanged(e);
        }
    }

    void SelectPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
            OnPropertyChanging(e);
    }

    void SetCount(int value) =>
        SetBackedProperty(ref count, in value, countPropertyChangingEventArgs, countPropertyChangedEventArgs);

    public override string ToString() =>
        $"reduction of mapping {source} with {Selector}";
}
