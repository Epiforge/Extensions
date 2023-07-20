namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableMethodCallExpression :
    ObservableExpression
{
    public ObservableMethodCallExpression(ExpressionObserver observer, MethodCallExpression methodCallExpression, bool deferEvaluation) :
        base(observer, methodCallExpression, deferEvaluation) =>
        MethodCallExpression = methodCallExpression;

    IReadOnlyList<ObservableExpression>? arguments;
    MethodInfo? method;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? @object;

    internal readonly MethodCallExpression MethodCallExpression;

    void ArgumentPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                DisposeValueIfNecessaryAndPossible();
                if (@object is not null)
                {
                    @object.PropertyChanged -= ObjectPropertyChanged;
                    @object.Dispose();
                }
                if (arguments is not null)
                    for (int i = 0, ii = arguments.Count; i < ii; ++i)
                    {
                        var argument = arguments[i];
                        argument.PropertyChanged -= ArgumentPropertyChanged;
                        argument.Dispose();
                    }
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void Evaluate()
    {
        try
        {
            var (objectFault, objectResult) = @object?.Evaluation ?? (null, null);
            if (objectFault is not null)
                Evaluation = (objectFault, defaultResult);
            else if (arguments?.Select(argument => argument.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } argumentFault)
                Evaluation = (argumentFault, defaultResult);
            else
                Evaluation = (null, method?.FastInvoke(objectResult, arguments?.Select(argument => argument.Evaluation.Result).ToArray() ?? Array.Empty<object?>()));
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        method is not null && observer.IsMethodReturnValueDisposed(method);

    protected override void OnInitialization()
    {
        var argumentsList = new List<ObservableExpression>();
        try
        {
            method = MethodCallExpression.Method;
            if (MethodCallExpression.Object is { } methodCallExpressionObject)
            {
                @object = observer.GetObservableExpression(methodCallExpressionObject, IsDeferringEvaluation);
                @object.PropertyChanged += ObjectPropertyChanged;
            }
            var methodCallExpressionArguments = MethodCallExpression.Arguments;
            for (int i = 0, ii = methodCallExpressionArguments.Count; i < ii; ++i)
            {
                var methodCallExpressionArgument = methodCallExpressionArguments[i];
                var argument = observer.GetObservableExpression(methodCallExpressionArgument, IsDeferringEvaluation);
                argument.PropertyChanged += ArgumentPropertyChanged;
                argumentsList.Add(argument);
            }
            arguments = argumentsList.AsReadOnly();
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
            if (@object is not null)
            {
                @object.PropertyChanged -= ObjectPropertyChanged;
                @object.Dispose();
            }
            for (int i = 0, ii = argumentsList.Count; i < ii; ++i)
            {
                var argument = argumentsList[i];
                argument.PropertyChanged -= ArgumentPropertyChanged;
                argument.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void ObjectPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();
}
