namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableNewArrayInitExpression :
    ObservableExpression
{
    public ObservableNewArrayInitExpression(ExpressionObserver observer, NewArrayExpression newArrayExpression, bool deferEvaluation) :
        base(observer, newArrayExpression, deferEvaluation) =>
        NewArrayExpression = newArrayExpression;

    Type? elementType;
    IReadOnlyList<ObservableExpression>? initializers;

    internal readonly NewArrayExpression NewArrayExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                if (initializers is not null)
                    for (int i = 0, ii = initializers.Count; i < ii; ++i)
                    {
                        var initializer = initializers[i];
                        initializer.PropertyChanged -= InitializerPropertyChanged;
                        initializer.Dispose();
                    }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void Evaluate()
    {
        if (initializers?.Select(initializer => initializer.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } initializerFault)
        {
            Evaluation = (initializerFault, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, initializerFault, "{NewArrayExpression} initializer faulted: {Fault}", NewArrayExpression, initializerFault);
        }
        else
        {
            var array = Array.CreateInstance(elementType!, initializers?.Count ?? 0);
            for (int i = 0, ii = initializers?.Count ?? 0; i < ii; ++i)
                array.SetValue(initializers?[i].Evaluation.Result, i);
            Evaluation = (null, array);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{NewArrayExpression} evaluated: {Value}", NewArrayExpression, array);
        }
    }

    void InitializerPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    protected override void OnInitialization()
    {
        var initializersList = new List<ObservableExpression>();
        try
        {
            elementType = NewArrayExpression.Type.GetElementType();
            var newArrayExpressionInitializers = NewArrayExpression.Expressions;
            for (int i = 0, ii = newArrayExpressionInitializers.Count; i < ii; ++i)
            {
                var newArrayExpressionInitializer = newArrayExpressionInitializers[i];
                var initializer = observer.GetObservableExpression(newArrayExpressionInitializer, IsDeferringEvaluation);
                initializer.PropertyChanged += InitializerPropertyChanged;
                initializersList.Add(initializer);
            }
            initializers = initializersList;
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            for (int i = 0, ii = initializersList.Count; i < ii; ++i)
            {
                var initializer = initializersList[i];
                initializer.PropertyChanged -= InitializerPropertyChanged;
                initializer.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }   
    }
}
