namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryGenericEnumerable<TElement> :
    ObservableCollectionQuery<TElement>
{
    public ObservableCollectionQueryGenericEnumerable(CollectionObserver collectionObserver, IEnumerable<TElement> enumerable) :
        base(collectionObserver) =>
        Enumerable = enumerable;

    internal readonly IEnumerable<TElement> Enumerable;

    public override TElement this[int index] =>
        Enumerable.ElementAt(index);

    public override int Count =>
        Enumerable.Count();

    internal override bool HasEnumerationPenalty =>
        false;

    internal override bool HasIndexerPenalty =>
        true;

    void CollectionChangedNotifierCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnCollectionChanged(e);

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = collectionObserver.QueryDisposed(this);
            if (removedFromCache)
            {
                if (Enumerable is INotifyCollectionChanged collectionChangedNotifier)
                    collectionChangedNotifier.CollectionChanged -= CollectionChangedNotifierCollectionChanged;
                if (collectionObserver.ExpressionObserver.Logger is { } logger && logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace("Disposed observation of enumerable of {ElementTypeFullName} elements (hash code {HashCode})", typeof(TElement).FullName, Enumerable.GetHashCode());
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        Enumerable.GetEnumerator();

    protected override void OnInitialization()
    {
        if (Enumerable is INotifyCollectionChanged collectionChangedNotifier)
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
        if (collectionObserver.ExpressionObserver.Logger is { } logger && logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace("Initialized observation of enumerable of {ElementTypeFullName} elements (hash code {HashCode})", typeof(TElement).FullName, Enumerable.GetHashCode());
    }

    public override string ToString() =>
        $"enumerable of {typeof(TElement).FullName} elements (hash code {Enumerable.GetHashCode()})";
}