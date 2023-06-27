namespace Epiforge.Extensions.Expressions.Observable;

class ObservableBinaryExpression :
    ObservableExpression
{
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

    public ObservableBinaryExpression(ExpressionObserver observer, BinaryExpression binaryExpression, bool deferEvaluation) :
        base(observer, binaryExpression, deferEvaluation) =>
        BinaryExpression = binaryExpression;

    BinaryOperationDelegate? @delegate;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    protected ObservableExpression? left;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    protected ObservableExpression? right;

    internal readonly BinaryExpression BinaryExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.Disposed(this);
            if (removedFromCache)
            {
                DisposeValueIfNecessaryAndPossible();
                if (left is not null)
                {
                    left.PropertyChanged -= LeftPropertyChanged;
                    left.Dispose();
                }
                if (right is not null)
                {
                    right.PropertyChanged -= RightPropertyChanged;
                    right.Dispose();
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
            var (leftFault, leftResult) = left?.Evaluation ?? (null, null);
            if (leftFault is not null)
                Evaluation = (leftFault, defaultResult);
            else
            {
                var (rightFault, rightResult) = right?.Evaluation ?? (null, null);
                if (rightFault is not null)
                    Evaluation = (rightFault, defaultResult);
                else
                    Evaluation = (null, @delegate?.Invoke(leftResult, rightResult));
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
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
            left.PropertyChanged += LeftPropertyChanged;
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
            right.PropertyChanged += RightPropertyChanged;
            if (Expression.NodeType is not ExpressionType.Coalesce)
                EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeValueIfNecessaryAndPossible();
            if (left is not null)
            {
                left.PropertyChanged -= LeftPropertyChanged;
                left.Dispose();
            }
            if (right is not null)
            {
                right.PropertyChanged -= RightPropertyChanged;
                right.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void LeftPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();

    void RightPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();
}
