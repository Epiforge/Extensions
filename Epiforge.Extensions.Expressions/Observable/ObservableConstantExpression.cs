namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableConstantExpression(ExpressionObserver observer, ConstantExpression constantExpression, bool deferEvaluation) :
    ObservableExpression(observer, constantExpression, deferEvaluation)
{
    bool valueNotifies;

    internal readonly ConstantExpression ConstantExpression = constantExpression;

    internal override bool CanChange =>
        valueNotifies;

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
        {
            var value = Evaluation.Result;
            if (observer.ConstantExpressionsListenForDictionaryChanged && value is INotifyDictionaryChanged dictionaryChanged)
                dictionaryChanged.DictionaryChanged -= ValueChanged;
            else if (observer.ConstantExpressionsListenForCollectionChanged && value is INotifyCollectionChanged collectionChanged)
                collectionChanged.CollectionChanged -= ValueChanged;
            RemovedFromCache();
        }
        return removedFromCache;
    }

    protected override void OnInitialization()
    {
        var value = ConstantExpression.Value;
        Evaluation = (null, value);
        if (observer.ConstantExpressionsListenForDictionaryChanged && value is INotifyDictionaryChanged dictionaryChanged)
        {
            dictionaryChanged.DictionaryChanged += ValueChanged;
            valueNotifies = true;
        }
        else if (observer.ConstantExpressionsListenForCollectionChanged && value is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged += ValueChanged;
            valueNotifies = true;
        }
    }

    void ValueChanged(object? sender, EventArgs e)
    {
        using var propagation = new PropagationScope();
        NotifyDependentsOfValueContentsChanged();
    }
}
