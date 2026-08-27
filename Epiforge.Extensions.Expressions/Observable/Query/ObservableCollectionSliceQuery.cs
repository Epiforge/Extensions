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
    int sourceCount;

    public override TElement this[int index]
    {
        get
        {
            int offset;
            lock (access)
            {
                if (index < 0 || index >= count)
                    throw ExceptionHelper.IndexArgumentWasOutOfRange;
                (offset, _) = GetOffsetAndLength();
            }
            return source[offset + index];
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
        int offset, length;
        lock (access)
            (offset, length) = GetOffsetAndLength();
        return source.Skip(offset).Take(length).GetEnumerator();
    }

    (int offset, int length) GetOffsetAndLength()
    {
        var lowerOffset = Math.Clamp(Range.Start.GetOffset(sourceCount), 0, sourceCount);
        var upperOffset = Math.Clamp(Range.End.GetOffset(sourceCount), 0, sourceCount);
        return (lowerOffset, Math.Max(0, upperOffset - lowerOffset));
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
        sourceCount = source.Count;
        var (_, length) = GetOffsetAndLength();
        SetBackedProperty(ref count, in length, countPropertyChangingEventArgs, countPropertyChangedEventArgs);
    }

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            if (e.Action != NotifyCollectionChangedAction.Move)
                ResetCount();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
