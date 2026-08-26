namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionAverageQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Expression<Func<TElement, TResult>> selector) :
    ObservableCollectionScalarQuery<TElement, TResult>(collectionObserver, observableCollectionQuery)
{
#if IS_NET_9_0_OR_GREATER
    readonly Lock access = new();
#else
    readonly object access = new();
#endif

    Func<TResult, TResult, TResult>? add;
    Func<TResult, TResult, TResult>? divide;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<TResult>? select;
    TResult sum = default!;
    Func<TResult, TResult, TResult>? subtract;

    internal readonly Expression<Func<TElement, TResult>> Selector = selector;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                if (select is not null)
                {
                    select.CollectionChanged -= SelectCollectionChanged;
                    select.PropertyChanged -= SelectPropertyChanged;
                    select.Dispose();
                }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void OnInitialization()
    {
        add = GenericAddition<TResult, TResult, TResult>.Instance;
        divide = GenericDivision<TResult, TResult, TResult>.Instance;
        subtract = GenericSubtraction<TResult, TResult, TResult>.Instance;
        select = observableCollectionQuery.ObserveSelect(Selector);
        lock (access)
            Recompute();
        select.CollectionChanged += SelectCollectionChanged;
        select.PropertyChanged += SelectPropertyChanged;
    }

    void Publish()
    {
        if (select!.OperationFault is { } selectFault)
            Evaluation = (selectFault, default)!;
        else
        {
            var count = select.Count;
            Evaluation = count > 0 ? (null, divide!(sum, (TResult)Convert.ChangeType(count, typeof(TResult)))) : (ExceptionHelper.SequenceContainsNoElements, default!);
        }
    }

    void Recompute()
    {
        sum = select!.Aggregate(default!, add!);
        Publish();
    }

    void SelectCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (access)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    var oldItems = e.OldItems?.Cast<TResult>() ?? [];
                    var newItems = e.NewItems?.Cast<TResult>() ?? [];
                    sum = subtract!(sum, oldItems.Aggregate(default!, add!));
                    sum = add!(sum, newItems.Aggregate(default!, add!));
                    Publish();
                    break;
                case NotifyCollectionChangedAction.Reset:
                    Recompute();
                    break;
            }
        }
    }

    void SelectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            lock (access)
                Recompute();
    }

    public override string ToString() =>
        $"average of {observableCollectionQuery}";
}
