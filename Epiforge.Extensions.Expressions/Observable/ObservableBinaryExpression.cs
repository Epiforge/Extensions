namespace Epiforge.Extensions.Expressions.Observable;

class ObservableBinaryExpression(ExpressionObserver observer, BinaryExpression binaryExpression, bool deferEvaluation) :
    ObservableExpression(observer, binaryExpression, deferEvaluation),
    IObservableExpressionDependent
{
    #region Delegates

    delegate object? BinaryOperationDelegate(object? left, object? right);

    #endregion Delegates

    sealed record ImplementationsKey(ExpressionType NodeType, Type LeftType, Type RightType, Type ReturnValueType, bool IsLiftedToNull, MethodInfo? Method);

    static readonly ConcurrentDictionary<ImplementationsKey, BinaryOperationDelegate> implementations = new();

    static BinaryOperationDelegate ImplementationsValueFactory(ImplementationsKey key)
    {
        var leftParameter = Expression.Parameter(typeof(object));
        var rightParameter = Expression.Parameter(typeof(object));
        var leftConversion = Expression.Convert(leftParameter, key.LeftType);
        var rightConversion = Expression.Convert(rightParameter, key.RightType);
        return Expression.Lambda<BinaryOperationDelegate>(Expression.Convert(key.Method is null ? Expression.MakeBinary(key.NodeType, leftConversion, rightConversion) : Expression.MakeBinary(key.NodeType, leftConversion, rightConversion, key.IsLiftedToNull, key.Method), typeof(object)), leftParameter, rightParameter).Compile();
    }

    BinaryOperationDelegate? @delegate;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    protected ObservableExpression? left;
    ObservableExpressionSubscription? leftSubscription;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    protected ObservableExpression? right;
    ObservableExpressionSubscription? rightSubscription;

    internal readonly BinaryExpression BinaryExpression = binaryExpression;

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
        {
            DisposeValueIfNecessaryAndPossible();
            if (left is not null)
            {
                if (leftSubscription is { } leftDependency)
                    left.UnsubscribeDependent(leftDependency);
                left.Dispose();
            }
            if (right is not null)
            {
                if (rightSubscription is { } rightDependency)
                    right.UnsubscribeDependent(rightDependency);
                right.Dispose();
            }
            RemovedFromCache();
        }
        return removedFromCache;
    }

    protected override void Evaluate()
    {
        try
        {
            var (leftFault, leftResult) = left?.Evaluation ?? (null, null);
            if (leftFault is not null)
            {
                Evaluation = (leftFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, leftFault, "{BinaryExpression} left-hand operand faulted: {Fault}", BinaryExpression, leftFault);
            }
            else
            {
                var (rightFault, rightResult) = right?.Evaluation ?? (null, null);
                if (rightFault is not null)
                {
                    Evaluation = (rightFault, defaultResult);
                    observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, rightFault, "{BinaryExpression} right-hand operand faulted: {Fault}", BinaryExpression, rightFault);
                }
                else
                {
                    var value = @delegate?.Invoke(leftResult, rightResult);
                    Evaluation = (null, value);
                    observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{BinaryExpression} evaluated: {Value}", BinaryExpression, value);
                }
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{BinaryExpression} faulted: {Fault}", BinaryExpression, ex);
        }
    }

    protected override bool GetShouldValueBeDisposed() =>
        BinaryExpression.Method is { } method && observer.IsMethodReturnValueDisposed(method);

    protected override void OnInitialization()
    {
        try
        {
            var binaryExpression = BinaryExpression;
            left = observer.GetObservableExpression(binaryExpression.Left, IsDeferringEvaluation);
            if (left.CanChange)
                leftSubscription = left.SubscribeDependent(this);
            switch (Expression.NodeType)
            {
                case ExpressionType.Coalesce:
                case ExpressionType.AndAlso or ExpressionType.OrElse when Expression.Type == typeof(bool):
                    right = observer.GetObservableExpression(binaryExpression.Right, true);
                    break;
                default:
                    right = observer.GetObservableExpression(binaryExpression.Right, IsDeferringEvaluation);
                    @delegate = implementations.GetOrAdd(new ImplementationsKey(binaryExpression.NodeType, binaryExpression.Left.Type, binaryExpression.Right.Type, binaryExpression.Type, binaryExpression.IsLiftedToNull, binaryExpression.Method), ImplementationsValueFactory);
                    break;
            }
            if (right.CanChange)
                rightSubscription = right.SubscribeDependent(this);
            if (Expression.NodeType is not ExpressionType.Coalesce)
                EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
            if (left is not null)
            {
                if (leftSubscription is { } leftDependency)
                    left.UnsubscribeDependent(leftDependency);
                left.Dispose();
            }
            if (right is not null)
            {
                if (rightSubscription is { } rightDependency)
                    right.UnsubscribeDependent(rightDependency);
                right.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        Evaluate();
}
