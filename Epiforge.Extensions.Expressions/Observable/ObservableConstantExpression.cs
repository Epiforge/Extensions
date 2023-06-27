namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableConstantExpression :
    ObservableExpression
{
    public ObservableConstantExpression(ExpressionObserver observer, ConstantExpression constantExpression, bool deferEvaluation) :
        base(observer, constantExpression, deferEvaluation) =>
        ConstantExpression = constantExpression;

    internal readonly ConstantExpression ConstantExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.Disposed(this);
            if (removedFromCache)
            {
                var value = Evaluation.Result;
                if (observer.ConstantExpressionsListenForDictionaryChanged && value is INotifyDictionaryChanged dictionaryChanged)
                    dictionaryChanged.DictionaryChanged -= ValueChanged;
                else if (observer.ConstantExpressionsListenForCollectionChanged && value is INotifyCollectionChanged collectionChanged)
                    collectionChanged.CollectionChanged -= ValueChanged;
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void OnInitialization()
    {
        var value = ConstantExpression.Value;
        Evaluation = (null, value);
        if (observer.ConstantExpressionsListenForDictionaryChanged && value is INotifyDictionaryChanged dictionaryChanged)
            dictionaryChanged.DictionaryChanged += ValueChanged;
        else if (observer.ConstantExpressionsListenForCollectionChanged && value is INotifyCollectionChanged collectionChanged)
            collectionChanged.CollectionChanged += ValueChanged;
    }

    void ValueChanged(object? sender, EventArgs e) =>
        OnPropertyChanged(EvaluationPropertyChangedEventArgs);
}
