namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableNewExpression :
    ObservableExpression
{
    public ObservableNewExpression(ExpressionObserver observer, NewExpression newExpression, bool deferEvaluation) :
        base(observer, newExpression, deferEvaluation) =>
        NewExpression = newExpression;

    IReadOnlyList<ObservableExpression>? arguments;
    ConstructorInfo? constructor;
    EquatableList<Type> constructorParameterTypes;

    internal readonly NewExpression NewExpression;

    void ArgumentPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.Disposed(this);
            if (removedFromCache)
            {
                DisposeValueIfNecessaryAndPossible();
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
            if (arguments?.Select(argument => argument.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } argumentFault)
                Evaluation = (argumentFault, defaultResult);
            else
                Evaluation = (null, constructor is not null ? constructor.FastInvoke(arguments?.Select(argument => argument.Evaluation.Result).ToArray() ?? Array.Empty<object?>()) : Activator.CreateInstance(NewExpression.Type, arguments?.Select(argument => argument.Evaluation.Result).ToArray() ?? Array.Empty<object?>()));
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        observer.IsConstructedTypeDisposed(NewExpression.Type, constructorParameterTypes);

    protected override void OnInitialization()
    {
        var argumentsList = new List<ObservableExpression>();
        try
        {
            constructor = NewExpression.Constructor;
            var newExpressionArguments = NewExpression.Arguments;
            for (int i = 0, ii = newExpressionArguments.Count; i < ii; ++i)
            {
                var newExpressionArgument = newExpressionArguments[i];
                var argument = observer.GetObservableExpression(newExpressionArgument, IsDeferringEvaluation);
                argument.PropertyChanged += ArgumentPropertyChanged;
                argumentsList.Add(argument);
            }
            arguments = argumentsList.AsReadOnly();
            constructorParameterTypes = new(arguments.Select(arguments => arguments.Expression.Type));
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
            for (int i = 0, ii = argumentsList.Count; i < ii; ++i)
            {
                var argument = argumentsList[i];
                argument.PropertyChanged -= ArgumentPropertyChanged;
                argument.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
