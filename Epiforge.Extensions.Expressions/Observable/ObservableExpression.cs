namespace Epiforge.Extensions.Expressions.Observable;

abstract class ObservableExpression :
    PlainSyncDisposable
{
    sealed class FaultedEvaluation(Exception fault)
    {
        internal readonly Exception Fault = fault;
    }

    const int evaluationRequested = 1;
    const int flags = evaluationRequested | initializing | retired | tearDownPending;
    const int initializing = 2;
    const int ownerShift = 4;
    const int retired = 4;
    const int tearDownPending = 8;

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
        evaluation = defaultResult;
    }

    protected readonly object? defaultResult;
    int deferringEvaluation;
    /// <summary>
    /// The thread evaluating or building this node, shifted clear of four flags: that an evaluation was asked for while it did, that it is building rather than evaluating, that the node's last observation has been released, and that the thread evaluating is to tear the node down when it lets go
    /// </summary>
    int evaluating;
    Expression? expression;
#if IS_NET_9_0_OR_GREATER
    readonly Lock dependentsAccess = new();
#else
    readonly object dependentsAccess = new();
#endif
    ObservableExpressionSubscription? firstDependent;
    ObservableExpressionSubscription? lastDependent;
    /// <summary>
    /// What the node evaluated to, held as one reference so that a thread reading it while another writes it reads one evaluation or the other and never a fault from one with a result from the other: the result itself, or the fault wrapped
    /// </summary>
    object? evaluation;
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
            return CurrentEvaluation;
        }
        protected set
        {
            var (currentFault, currentResult) = CurrentEvaluation;
            if (!ReferenceEquals(currentFault, value.Fault) || !resultEqualityComparer.Equals(currentResult, value.Result))
            {
                Volatile.Write(ref evaluation, value.Fault is { } fault ? new FaultedEvaluation(fault) : value.Result);
                NotifyDependentsChanged();
                DisposeIfNecessaryAndPossible(currentResult);
            }
        }
    }

    protected bool IsDeferringEvaluation =>
        Volatile.Read(ref deferringEvaluation) != 0;

    /// <summary>
    /// Gets whether this thread is evaluating or building this node, in which case what it is doing will read whatever is announcing to it
    /// </summary>
    private protected bool IsEvaluatingOnThisThread =>
        (Volatile.Read(ref evaluating) & ~flags) == Environment.CurrentManagedThreadId << ownerShift;

    void DisposeIfNecessaryAndPossible(object? value)
    {
        if (GetShouldValueBeDisposed())
        {
            if (!observer.PreferAsyncDisposal && value is IDisposable preferredDisposable)
                preferredDisposable.Dispose();
            else if (value is IAsyncDisposable asyncDisposable)
            {
                if (observer.BlockOnAsyncDisposal)
                    Task.Run(() => asyncDisposable.DisposeAsync().AsTask()).Wait();
                else
                    Task.Run(async () => await asyncDisposable.DisposeAsync().ConfigureAwait(false));
            }
            else if (value is IDisposable disposable)
                disposable.Dispose();
        }
    }

    protected void DisposeValueIfNecessaryAndPossible() =>
        DisposeIfNecessaryAndPossible(CurrentEvaluation.Result);

    protected virtual void Evaluate()
    {
    }

    internal void EvaluateIfDeferred()
    {
        if (Volatile.Read(ref deferringEvaluation) != 0 && Interlocked.Exchange(ref deferringEvaluation, 0) != 0)
            EvaluateAsOwner();
    }

    protected void EvaluateIfNotDeferred()
    {
        if (Volatile.Read(ref deferringEvaluation) == 0)
            EvaluateAsOwner();
    }

    /// <summary>
    /// Evaluates unless this observation is already evaluating or being built, declining when that is on this thread and leaving it to evaluate once more when that is on another
    /// </summary>
    /// <remarks>
    /// Reading a deferred observation's evaluation both resolves it and announces that its value changed, and the thing which read it is often a dependent part way through its own evaluation, on the line which reads it. Left alone, that announcement re-enters the dependent's evaluation, which completes against the now resolved dependency, after which the outer evaluation carries on and does the same work a second time. Both produce the same value, which is why nothing has reported it, and for a node producing something the observer disposes of it makes and discards one more of them than the expression requires.
    /// An evaluation in progress on this thread has not yet finished reading what it depends on, so it will read what the announcement was telling it, which is what makes declining safe rather than a dropped notification; a source announcing during the evaluation which reads it is the exception, and asks to be read again instead. An evaluation in progress on another thread may already have read what changed, so a request from elsewhere is never declined: the thread evaluating evaluates again once it has finished, and before it lets go, so the last evaluation always begins after the last change it was told of. A node being built is treated the same way, so that nothing evaluates a node another thread has not finished building
    /// </remarks>
    private protected void EvaluateOnce() =>
        EvaluateOnce(false);

    private protected void EvaluateOnce(bool rerunOnReentry)
    {
        var owner = Environment.CurrentManagedThreadId << ownerShift;
        while (true)
        {
            var state = Volatile.Read(ref evaluating);
            if (state == 0)
            {
                if (ExchangeState(owner, 0))
                    break;
                continue;
            }
            if ((state & retired) != 0 || (state & ~flags) == owner && !rerunOnReentry)
                return;
            if ((state & evaluationRequested) != 0 || ExchangeState(state | evaluationRequested, state))
                return;
        }
        EvaluateAndRelinquish(owner);
    }

    /// <summary>
    /// Replaces the evaluation state with the specified value if it is the specified comparand, interlocked unless the observer's observations are confined to one thread at a time
    /// </summary>
    bool ExchangeState(int value, int comparand)
    {
        if (observer.IsThreadSafe)
            return Interlocked.CompareExchange(ref evaluating, value, comparand) == comparand;
        if (evaluating != comparand)
            return false;
        evaluating = value;
        return true;
    }

    void EvaluateAndRelinquish(int owner)
    {
        try
        {
            Evaluate();
        }
        catch
        {
            Abandon();
            throw;
        }
        Relinquish(owner);
    }

    /// <summary>
    /// Lets go of evaluating because an evaluation threw, keeping the node retired if it was and tearing it down if that was left to this thread
    /// </summary>
    void Abandon()
    {
        var state = Volatile.Read(ref evaluating);
        while (!ExchangeState(state & (retired | tearDownPending), state))
            state = Volatile.Read(ref evaluating);
        if ((state & tearDownPending) != 0)
        {
            Volatile.Write(ref evaluating, retired);
            TearDown();
        }
    }

    /// <summary>
    /// Evaluates for the node itself rather than for something announcing to it, which while the node is being built on this thread means evaluating now
    /// </summary>
    void EvaluateAsOwner()
    {
        if ((Volatile.Read(ref evaluating) & ~evaluationRequested) == (Environment.CurrentManagedThreadId << ownerShift | initializing))
            Evaluate();
        else
            EvaluateOnce();
    }

    /// <summary>
    /// Lets go of evaluating, first evaluating again for as long as something asked for an evaluation while this thread held it
    /// </summary>
    void Relinquish(int owner)
    {
        while (true)
        {
            var state = Volatile.Read(ref evaluating);
            if ((state & retired) != 0)
            {
                if (!ExchangeState(retired, state))
                    continue;
                if ((state & tearDownPending) != 0)
                    TearDown();
                return;
            }
            if ((state & evaluationRequested) == 0)
            {
                if (ExchangeState(0, state))
                    return;
                continue;
            }
            if (!ExchangeState(owner, state))
                continue;
            try
            {
                Evaluate();
            }
            catch
            {
                Abandon();
                throw;
            }
        }
    }

    /// <summary>
    /// Tears the node down now that its last observation has been released, unless another thread is evaluating it, in which case that thread tears it down when it lets go, so that nothing the evaluation attaches to or makes outlives the node and nothing is disposed of twice
    /// </summary>
    /// <remarks>
    /// Waiting for the other thread instead would be simpler, and would deadlock wherever that thread's evaluation, or something it notifies, waits on the thread releasing, as a handler marshalling to a user interface thread which is disposing of an observation does
    /// </remarks>
    private protected void Retire()
    {
        var owner = Environment.CurrentManagedThreadId << ownerShift;
        while (true)
        {
            var state = Volatile.Read(ref evaluating);
            if (state == 0 || (state & ~flags) == owner)
            {
                if (!ExchangeState(state | retired, state))
                    continue;
                TearDown();
                return;
            }
            if (ExchangeState(state | retired | tearDownPending, state))
                return;
        }
    }

    /// <summary>
    /// Releases what the node attached to, observes and made, once and on whichever thread <see cref="Retire"/> leaves it to
    /// </summary>
    private protected abstract void TearDown();

    protected virtual bool GetShouldValueBeDisposed() =>
        false;

    /// <summary>
    /// Builds the node while holding its evaluation, so that an announcement reaching it before it is built asks for an evaluation once it is rather than evaluating what is not yet there
    /// </summary>
    internal void Initialize()
    {
        var owner = Environment.CurrentManagedThreadId << ownerShift;
        Volatile.Write(ref evaluating, owner | initializing);
        try
        {
            OnInitialization();
        }
        catch
        {
            Abandon();
            throw;
        }
        observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionInitialized, "Initialized observation of {Expression}", Expression);
        Relinquish(owner);
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
        Decode(Volatile.Read(ref evaluation));

    internal object? CurrentState =>
        Volatile.Read(ref evaluation);

    internal (Exception? Fault, object? Result) Decode(object? state) =>
        state is FaultedEvaluation faulted ? (faulted.Fault, defaultResult) : (null, state);

    protected abstract void OnInitialization();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemovedFromCache() =>
        observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionDisposed, "Disposed observation of {Expression}", Expression);

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
        result = CurrentEvaluation.Result;
        return true;
    }

    /// <summary>
    /// Releases one observation of this node, one release at a time, since the disposal a release ends in ignores a call made while another is under way, and a release ignored is an observation never released
    /// </summary>
    internal virtual void Release()
    {
        if (!observer.IsThreadSafe)
        {
            Dispose();
            return;
        }
        lock (dependentsAccess)
            Dispose();
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

    /// <summary>
    /// Stands in for an evaluation not yet read, so that whichever of construction and a first announcement reads the node's evaluation last is the one kept, since construction subscribes before it reads and an announcement can arrive in between on another thread
    /// </summary>
    static readonly object unread = new();

    protected ScopedObservableExpression(ExpressionObserver observer, Expression? expression, ObservableExpression observableExpression, IReadOnlyList<object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(observableExpression);
        this.arguments = arguments;
        this.observer = observer;
        this.expression = expression;
        this.observableExpression = observableExpression;
        if (this.observableExpression.CanChange)
            subscription = this.observableExpression.SubscribeDependent(this);
        if (!observer.IsThreadSafe)
            evaluationState = observableExpression.CurrentState;
        else
            Interlocked.CompareExchange(ref evaluationState, observableExpression.CurrentState, unread);
    }

    private protected readonly ObservableExpression observableExpression;
    readonly ExpressionObserver observer;
    IReadOnlyList<object?>? arguments;
    int disposed;
    Expression? expression;
    object? evaluationState = unread;
    bool notificationForced;
    bool notificationPending;
    readonly ObservableExpressionSubscription? subscription;

    private protected (Exception? Fault, object? Result) evaluation =>
        observableExpression.Decode(Volatile.Read(ref evaluationState));

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
        observableExpression.Release();
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
        var currentState = observableExpression.CurrentState;
        if (ReferenceEquals(Volatile.Read(ref evaluationState), unread) && ReferenceEquals(Interlocked.CompareExchange(ref evaluationState, currentState, unread), unread))
            return;
        var current = observableExpression.Decode(currentState);
        var previous = evaluation;
        if (!notificationForced && FaultEquals(previous.Fault, current.Fault) && ResultEquals(previous.Result, current.Result))
            return;
        notificationForced = false;
        PropertyChanging?.Invoke(this, ObservableExpression.EvaluationPropertyChangingEventArgs);
        Volatile.Write(ref evaluationState, currentState);
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
