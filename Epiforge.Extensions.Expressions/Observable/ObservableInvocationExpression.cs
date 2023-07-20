namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableInvocationExpression :
    ObservableExpression
{
    public ObservableInvocationExpression(ExpressionObserver observer, InvocationExpression invocationExpression, bool deferEvaluation) :
        base(observer, invocationExpression, deferEvaluation) =>
        InvocationExpression = invocationExpression;

    IReadOnlyList<ObservableExpression>? observableArguments;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? observableDelegateExpression;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? observableExpression;

    internal readonly InvocationExpression InvocationExpression;

    void CreateObservableExpression()
    {
        switch (InvocationExpression.Expression)
        {
            case LambdaExpression lambdaExpression when observableArguments is not null:
                observableExpression = observer.GetObservableExpression(observer.ReplaceParameters(lambdaExpression, observableArguments.Select(observableArgument => observableArgument.Evaluation.Result).ToArray() ?? Array.Empty<object?>())!, IsDeferringEvaluation);
                break;
            case Expression expression when typeof(Delegate).IsAssignableFrom(expression.Type):
                var observableDelegateExpressionCreated = false;
                if (observableDelegateExpression is null)
                {
                    observableDelegateExpression = observer.GetObservableExpression(expression, IsDeferringEvaluation);
                    observableDelegateExpressionCreated = true;
                }
                if (observableDelegateExpression.Evaluation.Result is Delegate @delegate)
                    observableExpression = observer.GetObservableExpression(@delegate.Target is { } target ? Expression.Call(Expression.Constant(target), @delegate.Method, InvocationExpression.Arguments) : Expression.Call(@delegate.Method, InvocationExpression.Arguments), IsDeferringEvaluation);
                if (observableDelegateExpressionCreated)
                    observableDelegateExpression.PropertyChanged += ObservableDelegateExpressionPropertyChanged;
                break;
            default:
                throw new NotSupportedException();
        }
        if (observableExpression is not null)
            observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        EvaluateIfNotDeferred();
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                if (observableExpression is not null)
                {
                    observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                    observableExpression.Dispose();
                }
                if (observableDelegateExpression is not null)
                {
                    observableDelegateExpression.PropertyChanged -= ObservableDelegateExpressionPropertyChanged;
                    observableDelegateExpression.Dispose();
                }
                if (observableArguments is not null)
                    for (int i = 0, ii = observableArguments.Count; i < ii; i++)
                    {
                        var obserableArgument = observableArguments[i];
                        obserableArgument.PropertyChanged -= ObservableArgumentPropertyChanged;
                        obserableArgument.Dispose();
                    }
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void Evaluate()
    {
        var (observableExpressionFault, observableExpressionResult) = observableExpression?.Evaluation ?? (null, null);
        if (observableExpressionFault is not null)
            Evaluation = (observableExpressionFault, defaultResult);
        else if (observableArguments?.Select(observableArgument => observableArgument.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } observableArgumentFault)
            Evaluation = (observableArgumentFault, defaultResult);
        else
            Evaluation = (null, observableExpressionResult);
    }

    void ObservableArgumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (observableExpression is not null)
        {
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
            observableExpression.Dispose();
            observableExpression = null;
        }
        if (observableArguments?.All(observableArgument => observableArgument.Evaluation.Fault is null) ?? true)
            CreateObservableExpression();
        else if (!IsDeferringEvaluation)
            Evaluate();
    }

    void ObservableDelegateExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (observableExpression is not null)
        {
            observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
            observableExpression.Dispose();
            observableExpression = null;
        }
        CreateObservableExpression();
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    protected override void OnInitialization()
    {
        var observableArgumentsList = new List<ObservableExpression>();
        try
        {
            if (InvocationExpression.Expression is LambdaExpression)
            {
                var invocationExpressionArguments = InvocationExpression.Arguments;
                for (int i = 0, ii = invocationExpressionArguments.Count; i < ii; ++i)
                {
                    var invocationExpressionArgument = invocationExpressionArguments[i];
                    var observableArgument = observer.GetObservableExpression(invocationExpressionArgument, IsDeferringEvaluation);
                    observableArgument.PropertyChanged += ObservableArgumentPropertyChanged;
                    observableArgumentsList.Add(observableArgument);
                }
                observableArguments = observableArgumentsList.ToImmutableArray();
            }
            CreateObservableExpression();
        }
        catch (Exception ex)
        {
            if (observableExpression is not null)
            {
                observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                observableExpression.Dispose();
            }
            if (observableDelegateExpression is not null)
            {
                observableDelegateExpression.PropertyChanged -= ObservableDelegateExpressionPropertyChanged;
                observableDelegateExpression.Dispose();
            }
            for (int i = 0, ii = observableArgumentsList.Count; i < ii; ++i)
            {
                var observableArgument = observableArgumentsList[i];
                observableArgument.PropertyChanged -= ObservableArgumentPropertyChanged;
                observableArgument.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
