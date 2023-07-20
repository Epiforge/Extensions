namespace Epiforge.Extensions.Expressions.Observable.Query;

class ObservableCollectionIndividualChangesQuery<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionIndividualChangesQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source) :
        base(collectionObserver)
    {
        access = new();
        elements = new();
        this.source = source;
    }

    readonly object access;
    readonly RangeObservableCollection<TElement> elements;
    readonly ObservableCollectionQuery<TElement> source;

    public override TElement this[int index]
    {
        get
        {
            lock (access)
                return elements[index];
        }
    }

    public override int Count
    {
        get
        {
            lock (access)
                return elements.Count;
        }
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                elements.CollectionChanged -= ElementsCollectionChanged;
                ((INotifyPropertyChanged)elements).PropertyChanged -= ElementsPropertyChanged;
                source.CollectionChanged -= SourceCollectionChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock (access)
            foreach (var element in elements)
                yield return element;
    }

    protected override void OnInitialization()
    {
        elements.Reset(source);
        elements.CollectionChanged += ElementsCollectionChanged;
        ((INotifyPropertyChanged)elements).PropertyChanged += ElementsPropertyChanged;
        source.CollectionChanged += SourceCollectionChanged;
    }

    void ElementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    void ElementsPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Reset)
            lock (access)
                elements.Reset(source);
        else if (e.Action is NotifyCollectionChangedAction.Move)
        {
            var count = e.OldItems?.Count ?? 0;
            if (e.OldStartingIndex != e.NewStartingIndex && count > 0)
            {
                var extractionIndex = e.OldStartingIndex;
                var insertionIndex = e.NewStartingIndex - 1;
                for (var i = 0; i < count; ++i)
                {
                    lock (access)
                        elements.Move(extractionIndex, ++insertionIndex);
                    if (e.OldStartingIndex > e.NewStartingIndex)
                        ++extractionIndex;
                }
            }
        }
        else if (e.Action is NotifyCollectionChangedAction.Replace && e.OldItems is { } replaceOldItems && replaceOldItems.Count > 0 && e.NewItems is { } replaceNewItems && replaceNewItems.Count > 0 && e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && e.OldStartingIndex == e.NewStartingIndex)
        {
            var index = e.OldStartingIndex;
            for (int i = 0, ii = Math.Max(replaceOldItems.Count, replaceNewItems.Count); i < ii; ++i)
                lock (access)
                {
                    if (i >= replaceOldItems.Count)
                        elements.Insert(index + i, (TElement)replaceNewItems[i]!);
                    else if (i >= replaceNewItems.Count)
                        elements.RemoveAt(replaceNewItems.Count + index);
                    else
                        elements[index + i] = (TElement)replaceNewItems[i]!;
                }
        }
        else
        {
            if (e.OldItems is { } oldItems && e.OldStartingIndex >= 0)
            {
                var count = oldItems.Count;
                if (count > 0)
                    for (var i = 0; i < count; ++i)
                        lock (access)
                            elements.RemoveAt(e.OldStartingIndex);
            }
            if (e.NewItems is { } newItems && e.NewStartingIndex >= 0)
            {
                var count = newItems.Count;
                if (count > 0)
                    for (var i = 0; i < count; ++i)
                        lock (access)
                            elements.Insert(e.NewStartingIndex + i, (TElement)newItems[i]!);
            }
        }
    }
}
