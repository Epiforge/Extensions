namespace Epiforge.Extensions.Expressions.Observable;

abstract class ObservableExpression :
    SyncDisposable
{
    internal static readonly PropertyChangedEventArgs EvaluationPropertyChangedEventArgs = new(nameof(Evaluation));
    internal static readonly PropertyChangingEventArgs EvaluationPropertyChangingEventArgs = new(nameof(Evaluation));

    protected ObservableExpression(ExpressionObserver observer, Expression expression, bool deferEvaluation)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(expression);
#else
        if (observer is null)
            throw new ArgumentNullException(nameof(observer));
        if (expression is null)
            throw new ArgumentNullException(nameof(expression));
#endif
        this.observer = observer;
        Expression = expression;
#if IS_NET_STANDARD_2_1_OR_GREATER
        var type = Expression.Type;
#else
        var expressionType = Expression.Type;
        var type = expressionType.Name.StartsWith("<") ? typeof(object) : expressionType;
#endif
        defaultResult = type.FastDefault();
        resultEqualityComparer = FastEqualityComparer.Get(type);
        deferringEvaluation = deferEvaluation;
        evaluation = (null, defaultResult);
    }

    protected readonly object? defaultResult;
    bool deferringEvaluation;
    readonly object deferringEvaluationAccess = new();
    (Exception? Fault, object? Result) evaluation;
    protected readonly ExpressionObserver observer;
    readonly FastEqualityComparer resultEqualityComparer;

    internal readonly Expression Expression;
    internal readonly object InitializationAccess = new();
    internal Exception? InitializationException;
    internal bool IsInitialized;
    internal int Observations;

    public (Exception? Fault, object? Result) Evaluation
    {
        get
        {
            EvaluateIfDeferred();
            return evaluation;
        }
        protected set
        {
            if (!ReferenceEquals(evaluation.Fault, value.Fault) || !resultEqualityComparer.Equals(evaluation.Result, value.Result))
            {
                var previousValue = evaluation.Result;
                OnPropertyChanging(EvaluationPropertyChangingEventArgs);
                evaluation = value;
                OnPropertyChanged(EvaluationPropertyChangedEventArgs);
                DisposeIfNecessaryAndPossible(previousValue);
            }
        }
    }

    protected bool IsDeferringEvaluation
    {
        get
        {
            lock (deferringEvaluationAccess)
                return deferringEvaluation;
        }
    }

    void DisposeIfNecessaryAndPossible(object? value)
    {
        if (GetShouldValueBeDisposed())
        {
            if (!observer.PreferAsyncDisposal && value is IDisposable preferredDisposable)
                preferredDisposable.Dispose();
            else if (value is IAsyncDisposable asyncDisposable)
            {
                if (observer.BlockOnAsyncDisposal)
                    asyncDisposable.DisposeAsync().AsTask().Wait();
                else
                    Task.Run(async () => await asyncDisposable.DisposeAsync().ConfigureAwait(false));
            }
            else if (value is IDisposable disposable)
                disposable.Dispose();
        }
    }

    protected void DisposeValueIfNecessaryAndPossible() =>
        DisposeIfNecessaryAndPossible(evaluation.Result);

    protected virtual void Evaluate()
    {
    }

    internal void EvaluateIfDeferred()
    {
        lock (deferringEvaluationAccess)
        {
            if (deferringEvaluation)
            {
                deferringEvaluation = false;
                Evaluate();
            }
        }
    }

    protected void EvaluateIfNotDeferred()
    {
        lock (deferringEvaluationAccess)
        {
            if (!deferringEvaluation)
                Evaluate();
        }
    }

    protected virtual bool GetShouldValueBeDisposed() =>
        false;

    internal void Initialize() =>
        OnInitialization();

    protected abstract void OnInitialization();

    public override string ToString() =>
        Expression.ToString();

    protected bool TryGetUndeferredResult(out object? result)
    {
        lock (deferringEvaluationAccess)
        {
            if (deferringEvaluation)
            {
                result = null;
                return false;
            }
        }
        result = evaluation.Result;
        return true;
    }
}

class ObservableExpression<TResult> :
    SyncDisposable,
    IObservableExpression<TResult>
{
    public ObservableExpression(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, IReadOnlyList<object?> arguments)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(observableExpression);
        ArgumentNullException.ThrowIfNull(arguments);
#else
        if (observer is null)
            throw new ArgumentNullException(nameof(observer));
        if (expression is null)
            throw new ArgumentNullException(nameof(expression));
        if (observableExpression is null)
            throw new ArgumentNullException(nameof(observableExpression));
        if (arguments is null)
            throw new ArgumentNullException(nameof(arguments));
#endif
        this.observer = observer;
        Expression = expression;
        this.observableExpression = observableExpression;
        this.observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        this.observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
        Arguments = arguments;
    }

    readonly ObservableExpression observableExpression;
    readonly ExpressionObserver observer;

    internal int Observations;
    internal readonly Expression Expression;

    public IReadOnlyList<object?> Arguments { get; }

    public (Exception? Fault, TResult Result) Evaluation
    {
        get
        {
            var (fault, result) = observableExpression.Evaluation;
            return (fault, (TResult)result!);
        }
    }

    public IExpressionObserver Observer =>
        observer;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var disregarded = observer.Disregard(this);
            if (disregarded)
            {
                observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                observableExpression.Dispose();
            }
            return disregarded;
        }
        return true;
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void ObservableExpressionPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    public override string ToString() =>
        Expression.ToString();
}

class ObservableExpression<TArgument, TResult> :
    SyncDisposable,
    IObservableExpression<TArgument, TResult>
{
    public ObservableExpression(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, TArgument argument)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(observableExpression);
#else
        if (observer is null)
            throw new ArgumentNullException(nameof(observer));
        if (expression is null)
            throw new ArgumentNullException(nameof(expression));
        if (observableExpression is null)
            throw new ArgumentNullException(nameof(observableExpression));
#endif
        this.observer = observer;
        Expression = expression;
        this.observableExpression = observableExpression;
        this.observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        this.observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
        Argument = argument;
        Arguments = new object?[] { argument };
    }

    readonly ObservableExpression observableExpression;
    readonly ExpressionObserver observer;

    internal int Observations;
    internal readonly Expression Expression;

    public IReadOnlyList<object?> Arguments { get; }

    public TArgument Argument { get; }

    public (Exception? Fault, TResult Result) Evaluation
    {
        get
        {
            var (fault, result) = observableExpression.Evaluation;
            return (fault, (TResult)result!);
        }
    }

    public IExpressionObserver Observer =>
        observer;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                observableExpression.Dispose();
            }
            return removedFromCache;
        }
        return true;
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void ObservableExpressionPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    public override string ToString() =>
        Expression.ToString();
}

class ObservableExpression<TArgument1, TArgument2, TResult> :
    SyncDisposable,
    IObservableExpression<TArgument1, TArgument2, TResult>
{
    public ObservableExpression(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, TArgument1 argument1, TArgument2 argument2)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(observableExpression);
#else
        if (observer is null)
            throw new ArgumentNullException(nameof(observer));
        if (expression is null)
            throw new ArgumentNullException(nameof(expression));
        if (observableExpression is null)
            throw new ArgumentNullException(nameof(observableExpression));
#endif
        this.observer = observer;
        Expression = expression;
        this.observableExpression = observableExpression;
        this.observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        this.observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
        Argument1 = argument1;
        Argument2 = argument2;
        Arguments = new object?[] { argument1, argument2 };
    }

    readonly ObservableExpression observableExpression;
    readonly ExpressionObserver observer;

    internal int Observations;
    internal readonly Expression Expression;

    public IReadOnlyList<object?> Arguments { get; }

    public TArgument1 Argument1 { get; }

    public TArgument2 Argument2 { get; }

    public (Exception? Fault, TResult Result) Evaluation
    {
        get
        {
            var (fault, result) = observableExpression.Evaluation;
            return (fault, (TResult)result!);
        }
    }

    public IExpressionObserver Observer =>
        observer;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                observableExpression.Dispose();
            }
            return removedFromCache;
        }
        return true;
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void ObservableExpressionPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    public override string ToString() =>
        Expression.ToString();
}

class ObservableExpression<TArgument1, TArgument2, TArgument3, TResult> :
    SyncDisposable,
    IObservableExpression<TArgument1, TArgument2, TArgument3, TResult>
{
    public ObservableExpression(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(observableExpression);
#else
        if (observer is null)
            throw new ArgumentNullException(nameof(observer));
        if (expression is null)
            throw new ArgumentNullException(nameof(expression));
        if (observableExpression is null)
            throw new ArgumentNullException(nameof(observableExpression));
#endif
        this.observer = observer;
        Expression = expression;
        this.observableExpression = observableExpression;
        this.observableExpression.PropertyChanged += ObservableExpressionPropertyChanged;
        this.observableExpression.PropertyChanging += ObservableExpressionPropertyChanging;
        Argument1 = argument1;
        Argument2 = argument2;
        Argument3 = argument3;
        Arguments = new object?[] { argument1, argument2, argument3 };
    }

    readonly ObservableExpression observableExpression;
    readonly ExpressionObserver observer;

    internal int Observations;
    internal readonly Expression Expression;

    public IReadOnlyList<object?> Arguments { get; }

    public TArgument1 Argument1 { get; }

    public TArgument2 Argument2 { get; }

    public TArgument3 Argument3 { get; }

    public (Exception? Fault, TResult Result) Evaluation
    {
        get
        {
            var (fault, result) = observableExpression.Evaluation;
            return (fault, (TResult)result!);
        }
    }

    public IExpressionObserver Observer =>
        observer;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                observableExpression.PropertyChanged -= ObservableExpressionPropertyChanged;
                observableExpression.PropertyChanging -= ObservableExpressionPropertyChanging;
                observableExpression.Dispose();
            }
            return removedFromCache;
        }
        return true;
    }

    void ObservableExpressionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e);

    void ObservableExpressionPropertyChanging(object? sender, PropertyChangingEventArgs e) =>
        OnPropertyChanging(e);

    public override string ToString() =>
        Expression.ToString();
}
