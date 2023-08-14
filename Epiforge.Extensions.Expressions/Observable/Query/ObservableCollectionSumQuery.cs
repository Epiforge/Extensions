namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSumQuery<TElement, TResult> :
    ObservableCollectionScalarQuery<TElement, TResult>
{
    public ObservableCollectionSumQuery(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Expression<Func<TElement, TResult>> selector) :
        base(collectionObserver, observableCollectionQuery)
    {
        access = new();
        Selector = selector;
    }

    readonly object access;

    Func<TResult, TResult, TResult>? add;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableCollectionQuery<TResult>? select;
    Func<TResult, TResult, TResult>? subtract;

    internal readonly Expression<Func<TElement, TResult>> Selector;

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

    void Evaluate()
    {
        lock (access)
            Evaluation = select!.OperationFault is { } selectFault ? (selectFault, default)! : (null, select.Aggregate(default(TResult)!, add!));
    }

    protected override void OnInitialization()
    {
        add = GenericAddition<TResult, TResult, TResult>.Instance;
        subtract = GenericSubtraction<TResult, TResult, TResult>.Instance;
        select = observableCollectionQuery.ObserveSelect(Selector);
        Evaluate();
        select.CollectionChanged += SelectCollectionChanged;
        select.PropertyChanged += SelectPropertyChanged;
    }

    void SelectCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                if (Evaluation.Fault is not null)
                    Evaluate();
                else
                {
                    var value = Evaluation.Result;
                    var oldItems = e.OldItems?.Cast<TResult>() ?? Enumerable.Empty<TResult>();
                    var newItems = e.NewItems?.Cast<TResult>() ?? Enumerable.Empty<TResult>();
                    if (oldItems is not null)
                        value = subtract!(value, oldItems.Aggregate(default!, add!));
                    if (newItems is not null)
                        value = add!(value, newItems.Aggregate(default!, add!));
                    Evaluation = (null, value);
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                Evaluate();
                break;
        }
    }

    void SelectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<TResult>.OperationFault))
            Evaluate();
    }

    public override string ToString() =>
        select is null ? $"sum of {observableCollectionQuery}" : $"sum of {select}";
}
