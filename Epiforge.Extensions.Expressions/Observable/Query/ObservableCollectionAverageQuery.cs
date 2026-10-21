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
        using var changeHold = HoldChangesOf(Unscoped(select));
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
            Evaluation = count > 0 ? (null, divide!(sum, CountAsResult(count))) : (OwnFault(ExceptionHelper.SequenceContainsNoElements), default!);
        }
    }

    /// <summary>
    /// Converts the count to the type of the average, directly for the types an average is commonly taken in, where the JIT removes the box the conversion appears to make, and through <see cref="Convert.ChangeType(object, Type)"/> otherwise, which keeps its overflow checking for the narrow types
    /// </summary>
    static TResult CountAsResult(int count)
    {
        if (typeof(TResult) == typeof(int))
            return (TResult)(object)count;
        if (typeof(TResult) == typeof(long))
            return (TResult)(object)(long)count;
        if (typeof(TResult) == typeof(double))
            return (TResult)(object)(double)count;
        if (typeof(TResult) == typeof(float))
            return (TResult)(object)(float)count;
        if (typeof(TResult) == typeof(decimal))
            return (TResult)(object)(decimal)count;
        if (typeof(TResult) == typeof(uint))
            return (TResult)(object)(uint)count;
        if (typeof(TResult) == typeof(ulong))
            return (TResult)(object)(ulong)count;
        return (TResult)Convert.ChangeType(count, typeof(TResult));
    }

    void Recompute()
    {
        sum = select!.Aggregate(default!, add!);
        Publish();
    }

    void SelectCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        lock (access)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    sum = subtract!(sum, ObservableCollectionSumQuery<TElement, TResult>.Total(e.OldItems, add!));
                    sum = add!(sum, ObservableCollectionSumQuery<TElement, TResult>.Total(e.NewItems, add!));
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
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            lock (access)
                Recompute();
    }

    public override string ToString() =>
        $"average of {observableCollectionQuery}";
}
