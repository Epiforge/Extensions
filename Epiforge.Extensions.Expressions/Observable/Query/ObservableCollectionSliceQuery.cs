namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSliceQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Range range) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
#if IS_NET_9_0_OR_GREATER
    readonly Lock access = new();
#else
    readonly object access = new();
#endif
    int count;
    internal readonly Range Range = range;

    public override TElement this[int index]
    {
        get
        {
            lock (access)
            {
                if (index < 0 || index >= Count)
                    throw ExceptionHelper.IndexArgumentWasOutOfRange;
                return source[Range.Start.GetOffset(source.Count) + index];
            }
        }
    }

    public override int Count =>
        count;

    internal override bool HasEnumerationPenalty =>
        source.HasEnumerationPenalty;

    internal override bool HasIndexerPenalty =>
        source.HasIndexerPenalty;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                source.CollectionChanged -= SourceCollectionChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator()
    {
        lock (access)
        {
            var (offset, length) = Range.GetOffsetAndLength(source.Count);
            return source.Skip(offset).Take(length).GetEnumerator();
        }
    }

    protected override void OnInitialization()
    {
        lock (access)
        {
            ResetCount();
            source.CollectionChanged += SourceCollectionChanged;
        }
    }

    void ResetCount()
    {
        var sourceCount = source.Count;
        var (offset, length) = Range.GetOffsetAndLength(sourceCount);
        SetBackedProperty(ref count, Math.Max(0, Math.Min(length, sourceCount - offset)), countPropertyChangingEventArgs, countPropertyChangedEventArgs);
    }

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            NotifyCollectionChangedEventArgs? eventArgs = null;
            if (e.Action is NotifyCollectionChangedAction.Reset)
                eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            else
            {
                var oldItemsCount = e.OldItems?.Count ?? 0;
                var newItemsCount = e.NewItems?.Count ?? 0;
                var (oldOffset, oldLength) = Range.GetOffsetAndLength(count);
                var oldUpperBound = oldOffset + oldLength;
                if (oldItemsCount is > 0 && e.OldStartingIndex < oldUpperBound || newItemsCount is > 0 && e.NewStartingIndex < oldUpperBound)
                    eventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            }
            if (eventArgs is not null)
            {
                if (eventArgs.Action != NotifyCollectionChangedAction.Move)
                    ResetCount();
                OnCollectionChanged(eventArgs);
            }
        }
    }
}
