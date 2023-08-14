using Microsoft.Extensions.Logging;

namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionQueryEnumerable :
    ObservableCollectionQuery<object?>
{
    public ObservableCollectionQueryEnumerable(CollectionObserver collectionObserver, IEnumerable enumerable) :
        base(collectionObserver) =>
        Enumerable = enumerable;

    internal readonly IEnumerable Enumerable;

    public override object? this[int index] =>
        Enumerable.Cast<object?>().ElementAt(index);

    public override int Count =>
        Enumerable.Cast<object?>().Count();

    internal override bool HasEnumerationPenalty =>
        true;

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
                    logger.LogTrace("Disposed observation of enumerable of object elements (hash code {HashCode})", Enumerable.GetHashCode());
            }
            return removedFromCache;
        }
        return true;

    }

    public override IEnumerator<object?> GetEnumerator() =>
        Enumerable.Cast<object?>().GetEnumerator();

    protected override void OnInitialization()
    {
        if (Enumerable is INotifyCollectionChanged collectionChangedNotifier)
            collectionChangedNotifier.CollectionChanged += CollectionChangedNotifierCollectionChanged;
        if (collectionObserver.ExpressionObserver.Logger is { } logger && logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace("Initialized observation of enumerable of object elements (hash code {HashCode})", Enumerable.GetHashCode());
    }

    public override string ToString() =>
        $"enumerable of object elements (hash code {Enumerable.GetHashCode()})";
}
