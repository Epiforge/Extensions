namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionAverageQuery<TElement, TResult>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> observableCollectionQuery, Expression<Func<TElement, TResult>> selector) :
    ObservableCollectionScalarQuery<TElement, TResult>(collectionObserver, observableCollectionQuery)
{
    Func<TResult, TResult, TResult>? divide;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    IObservableScalarQuery<TResult>? sum;

    internal readonly Expression<Func<TElement, TResult>> Selector = selector;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observableCollectionQuery.QueryDisposed(this);
            if (removedFromCache)
            {
                if (sum is not null)
                {
                    sum.PropertyChanged -= SumPropertyChanged;
                    sum.Dispose();
                }
                observableCollectionQuery.PropertyChanged -= ObservableCollectionQueryPropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    void Evaluate() =>
        Evaluation = sum!.Evaluation.Fault is { } sumFault ? (sumFault, default)! : observableCollectionQuery.Count is { } count && count > 0 ? (null, divide!(sum!.Evaluation.Result, (TResult)Convert.ChangeType(count, typeof(TResult)))) : (ExceptionHelper.SequenceContainsNoElements, default!);

    void ObservableCollectionQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ObservableCollectionQuery<>.Count))
            Evaluate();
    }

    protected override void OnInitialization()
    {
        divide = GenericDivision<TResult, TResult, TResult>.Instance;
        observableCollectionQuery.PropertyChanged += ObservableCollectionQueryPropertyChanged;
        sum = observableCollectionQuery.ObserveSum(Selector);
        sum.PropertyChanged += SumPropertyChanged;
        Evaluate();
    }

    void SumPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableScalarQuery<>.Evaluation))
            Evaluate();
    }

    public override string ToString() =>
        $"average of {observableCollectionQuery}";
}
