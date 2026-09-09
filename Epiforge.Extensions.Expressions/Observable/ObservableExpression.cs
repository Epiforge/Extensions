namespace Epiforge.Extensions.Expressions.Observable;

abstract class ObservableExpression :
    PlainSyncDisposable
{
    internal static readonly PropertyChangedEventArgs EvaluationPropertyChangedEventArgs = new(nameof(Evaluation));
    internal static readonly PropertyChangingEventArgs EvaluationPropertyChangingEventArgs = new(nameof(Evaluation));

    static readonly ConcurrentDictionary<Type, object?> sharedDefaults = new();

    /// <summary>
    /// Yields the default value of a type, which for a value type is a box shared by every node of that type, since a default is never mutated and is only ever compared by value; a null entry means the type is not one for which a box may be shared, in which case a fresh one is made, and a type whose default is itself null costs nothing to make again
    /// </summary>
    static object? DefaultResult(Type type) =>
        type.IsValueType && sharedDefaults.GetOrAdd(type, SharedDefaultsValueFactory) is { } shared ? shared : type.FastDefault();

    /// <summary>
    /// Yields the results of the specified expressions as an array, generic over the list rather than taking the interface so that a list which is a value type is not boxed to be read
    /// </summary>
    internal static object?[] EvaluationResults<TExpressions>(TExpressions expressions)
        where TExpressions : IReadOnlyList<ObservableExpression>
    {
        var count = expressions.Count;
        if (count == 0)
            return [];
        var results = new object?[count];
        for (var i = 0; i < count; ++i)
            results[i] = expressions[i].Evaluation.Result;
        return results;
    }

    /// <summary>
    /// Invokes through the specified invoker with the specified operands' values, without building an argument array where the arity is small enough that the invoker takes the values directly
    /// </summary>
    internal static object? Invoke(FastInvoker invoker, object? instance, IReadOnlyList<ObservableExpression>? expressions) =>
        (expressions?.Count ?? 0) switch
        {
            0 => invoker.Invoke(instance),
            1 => invoker.Invoke(instance, expressions![0].Evaluation.Result),
            2 => invoker.Invoke(instance, expressions![0].Evaluation.Result, expressions[1].Evaluation.Result),
            _ => invoker.Invoke(instance, EvaluationResults(expressions!))
        };

    /// <summary>
    /// Yields the fault of the first of the specified expressions which has one, reading no further, which is what makes this equivalent to the lazy sequence it replaces
    /// </summary>
    internal static Exception? FirstFault<TExpressions>(TExpressions expressions)
        where TExpressions : IReadOnlyList<ObservableExpression>
    {
        for (int i = 0, ii = expressions.Count; i < ii; ++i)
            if (expressions[i].Evaluation.Fault is { } fault)
                return fault;
        return null;
    }

    static object? SharedDefaultsValueFactory(Type type) =>
        ExpressionObserverOptions.CannotBeDisposed(type) ? type.FastDefault() : null;

    static Expression Validated(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return expression;
    }

    protected ObservableExpression(ExpressionObserver observer, Expression expression, bool deferEvaluation) :
        this(observer, Validated(expression).Type, deferEvaluation) =>
        this.expression = expression;

    private protected ObservableExpression(ExpressionObserver observer, Type type, bool deferEvaluation)
    {
        ArgumentNullException.ThrowIfNull(observer);
        this.observer = observer;
        defaultResult = DefaultResult(type);
        resultEqualityComparer = FastEqualityComparer.Get(type);
        deferringEvaluation = deferEvaluation ? 1 : 0;
        evaluation = (null, defaultResult);
    }

    protected readonly object? defaultResult;
    int deferringEvaluation;
    Expression? expression;
#if IS_NET_9_0_OR_GREATER
    readonly Lock dependentsAccess = new();
#else
    readonly object dependentsAccess = new();
#endif
    ObservableExpressionSubscription? firstDependent;
    ObservableExpressionSubscription? lastDependent;
    (Exception? Fault, object? Result) evaluation;
    protected readonly ExpressionObserver observer;
    readonly FastEqualityComparer resultEqualityComparer;

#if IS_NET_9_0_OR_GREATER
    internal Lock? InitializationAccess = new();
#else
    internal object? InitializationAccess = new();
#endif
    internal Exception? InitializationException;
    internal bool IsInitialized;
    internal int Observations;

    internal virtual bool CanChange =>
        true;

    internal Expression Expression =>
        expression ??= Materialize();

    private protected virtual Expression Materialize() =>
        throw new NotSupportedException("this observation was constructed without an expression and cannot produce one");

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
                evaluation = value;
                NotifyDependentsChanged();
                DisposeIfNecessaryAndPossible(previousValue);
            }
        }
    }

    protected bool IsDeferringEvaluation =>
        Volatile.Read(ref deferringEvaluation) != 0;

    void DisposeIfNecessaryAndPossible(object? value)
    {
        if (GetShouldValueBeDisposed())
            observer.DisposeIfPossible(value);
    }

    protected void DisposeValueIfNecessaryAndPossible() =>
        DisposeIfNecessaryAndPossible(evaluation.Result);

    protected virtual void Evaluate()
    {
    }

    internal void EvaluateIfDeferred()
    {
        if (Volatile.Read(ref deferringEvaluation) != 0 && Interlocked.Exchange(ref deferringEvaluation, 0) != 0)
            Evaluate();
    }

    protected void EvaluateIfNotDeferred()
    {
        if (Volatile.Read(ref deferringEvaluation) == 0)
            Evaluate();
    }

    protected virtual bool GetShouldValueBeDisposed() =>
        false;

    internal void Initialize()
    {
        OnInitialization();
        observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionInitialized, "Initialized observation of {Expression}", Expression);
    }

    private protected void NotifyDependentsChanged()
    {
        var current = Volatile.Read(ref firstDependent);
        while (current is not null)
        {
            var following = current.Next;
            if (!current.IsRemoved)
                current.Dependent.OnDependencyEvaluationChanged(this);
            current = following;
        }
    }

    private protected void NotifyDependentsOfValueContentsChanged()
    {
        var current = Volatile.Read(ref firstDependent);
        while (current is not null)
        {
            var following = current.Next;
            if (!current.IsRemoved)
                current.Dependent.OnDependencyValueContentsChanged(this);
            current = following;
        }
    }

    internal (Exception? Fault, object? Result) CurrentEvaluation =>
        evaluation;

    protected abstract void OnInitialization();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemovedFromCache() =>
        observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionDisposed, "Disposed observation of {Expression}", Expression);

    internal ObservableExpressionSubscription SubscribeDependent(IObservableExpressionDependent dependent)
    {
        ArgumentNullException.ThrowIfNull(dependent);
        var subscription = new ObservableExpressionSubscription(dependent);
        lock (dependentsAccess)
        {
            subscription.Previous = lastDependent;
            if (lastDependent is null)
                Volatile.Write(ref firstDependent, subscription);
            else
                lastDependent.Next = subscription;
            lastDependent = subscription;
        }
        return subscription;
    }

    public override string ToString() =>
        Expression.ToString();

    protected bool TryGetUndeferredResult(out object? result)
    {
        if (Volatile.Read(ref deferringEvaluation) != 0)
        {
            result = null;
            return false;
        }
        result = evaluation.Result;
        return true;
    }

    internal void UnsubscribeDependent(ObservableExpressionSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        lock (dependentsAccess)
        {
            if (subscription.IsRemoved)
                return;
            subscription.IsRemoved = true;
            if (subscription.Previous is null)
                Volatile.Write(ref firstDependent, subscription.Next);
            else
                subscription.Previous.Next = subscription.Next;
            if (subscription.Next is null)
                lastDependent = subscription.Previous;
            else
                subscription.Next.Previous = subscription.Previous;
            subscription.Previous = null;
        }
    }
}

abstract class ScopedObservableExpression :
    IObservableExpressionDependent
{
    /// <summary>
    /// Determines whether two faults are the same fault, which is by type and message rather than by identity because an expression which is re-evaluated while faulted throws a new exception every time, and announcing that as a change would make the number of notifications a consumer receives depend on how often the mechanism happens to re-evaluate
    /// </summary>
    static bool FaultEquals(Exception? x, Exception? y) =>
        ReferenceEquals(x, y) || x is not null && y is not null && x.GetType() == y.GetType() && x.Message == y.Message;

    protected ScopedObservableExpression(ExpressionObserver observer, Expression? expression, ObservableExpression observableExpression, IReadOnlyList<object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(observableExpression);
        this.arguments = arguments;
        this.observer = observer;
        this.expression = expression;
        this.observableExpression = observableExpression;
        evaluation = observableExpression.CurrentEvaluation;
        if (this.observableExpression.CanChange)
            subscription = this.observableExpression.SubscribeDependent(this);
    }

    private protected readonly ObservableExpression observableExpression;
    readonly ExpressionObserver observer;
    IReadOnlyList<object?>? arguments;
    int disposed;
    Expression? expression;
    private protected (Exception? Fault, object? Result) evaluation;
    bool notificationForced;
    bool notificationPending;
    readonly ObservableExpressionSubscription? subscription;

    internal Expression Expression =>
        expression ??= observableExpression.Expression;

    /// <summary>
    /// Gets the arguments the expression is observed with, made when something asks for it where the observation was given its arguments singly, since one is made per element of a query and nothing within the library reads it
    /// </summary>
    public IReadOnlyList<object?> Arguments
    {
        get
        {
            if (arguments is { } made)
                return made;
            Interlocked.CompareExchange(ref arguments, MakeArguments(), null);
            return arguments!;
        }
    }

    public bool IsDisposed =>
        disposed != 0;

    public IExpressionObserver Observer =>
        observer;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event PropertyChangingEventHandler? PropertyChanging;

    public event EventHandler? Disposed;

    public event EventHandler? Disposing;

    internal void ClearPendingNotification() =>
        notificationPending = false;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        var e = EventArgs.Empty;
        Disposing?.Invoke(this, e);
        if (subscription is { } dependency)
            observableExpression.UnsubscribeDependent(dependency);
        observableExpression.Dispose();
        Disposed?.Invoke(this, e);
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency)
    {
        if (Enlisted())
            return;
        RaiseIfEvaluationChanged();
    }

    void IObservableExpressionDependent.OnDependencyValueContentsChanged(ObservableExpression dependency)
    {
        notificationForced = true;
        if (Enlisted())
            return;
        RaiseIfEvaluationChanged();
    }

    bool Enlisted()
    {
        if (!PropagationScope.IsPropagating)
            return false;
        if (!notificationPending)
        {
            notificationPending = true;
            PropagationScope.Enlist(this);
        }
        return true;
    }

    void RaiseIfEvaluationChanged()
    {
        var current = observableExpression.CurrentEvaluation;
        if (!notificationForced && FaultEquals(evaluation.Fault, current.Fault) && ResultEquals(evaluation.Result, current.Result))
            return;
        notificationForced = false;
        PropertyChanging?.Invoke(this, ObservableExpression.EvaluationPropertyChangingEventArgs);
        evaluation = current;
        PropertyChanged?.Invoke(this, ObservableExpression.EvaluationPropertyChangedEventArgs);
    }

    private protected virtual IReadOnlyList<object?> MakeArguments() =>
        [];

    private protected abstract bool ResultEquals(object? x, object? y);

    internal void RaisePendingNotification()
    {
        if (!IsDisposed)
            RaiseIfEvaluationChanged();
    }

    public override string ToString() =>
        Expression.ToString();
}

class ScopedObservableExpression<TResult>(ExpressionObserver observer, Expression? expression, ObservableExpression observableExpression, IReadOnlyList<object?>? arguments) :
    ScopedObservableExpression(observer, expression, observableExpression, arguments),
    IObservableExpression<TResult>
{
    /// <summary>
    /// Whether results of this type are best compared as they arrive, already boxed, which is so for a value type that does not implement <see cref="IEquatable{T}"/> because its default comparer boxes one of them again to reach the very same comparison
    /// </summary>
    static readonly bool compareResultsBoxed = typeof(TResult).IsValueType && !typeof(IEquatable<TResult>).IsAssignableFrom(typeof(TResult));

    public (Exception? Fault, TResult Result) Evaluation
    {
        get
        {
            var (fault, result) = evaluation;
            return (fault, (TResult)result!);
        }
    }

    private protected override bool ResultEquals(object? x, object? y) =>
        x is null || y is null ? ReferenceEquals(x, y) : compareResultsBoxed ? x.Equals(y) : EqualityComparer<TResult>.Default.Equals((TResult)x, (TResult)y);
}

class ScopedObservableExpression<TArgument, TResult>(ExpressionObserver observer, Expression? expression, ObservableExpression observableExpression, TArgument argument) :
    ScopedObservableExpression<TResult>(observer, expression, observableExpression, null),
    IObservableExpression<TArgument, TResult>
{
    public TArgument Argument { get; } = argument;

    private protected override IReadOnlyList<object?> MakeArguments() =>
        [Argument];
}

class ScopedObservableExpression<TArgument1, TArgument2, TResult>(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, TArgument1 argument1, TArgument2 argument2) :
    ScopedObservableExpression<TResult>(observer, expression, observableExpression, null),
    IObservableExpression<TArgument1, TArgument2, TResult>
{
    public TArgument1 Argument1 { get; } = argument1;

    public TArgument2 Argument2 { get; } = argument2;

    private protected override IReadOnlyList<object?> MakeArguments() =>
        [Argument1, Argument2];
}

class ScopedObservableExpression<TArgument1, TArgument2, TArgument3, TResult>(ExpressionObserver observer, Expression expression, ObservableExpression observableExpression, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) :
    ScopedObservableExpression<TResult>(observer, expression, observableExpression, null),
    IObservableExpression<TArgument1, TArgument2, TArgument3, TResult>
{
    public TArgument1 Argument1 { get; } = argument1;

    public TArgument2 Argument2 { get; } = argument2;

    public TArgument3 Argument3 { get; } = argument3;

    private protected override IReadOnlyList<object?> MakeArguments() =>
        [Argument1, Argument2, Argument3];
}
