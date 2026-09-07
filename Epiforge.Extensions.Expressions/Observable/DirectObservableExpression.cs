namespace Epiforge.Extensions.Expressions.Observable;

abstract class DirectObservableExpression(ExpressionObserver observer, Type type) :
    ObservableExpression(observer, type, false)
{
    internal static object? Resolve(Expression expression, object? argument) =>
        expression switch
        {
            ConstantExpression constantExpression => constantExpression.Value,
            ParameterExpression => argument,
            MemberExpression { Member: FieldInfo field, Expression: null } => field.GetValue(null),
            MemberExpression { Member: FieldInfo field } memberExpression => Resolve(memberExpression.Expression!, argument) is { } target ? field.GetValue(target) : null,
            MemberExpression { Member: PropertyInfo property, Expression: null } => property.GetValue(null),
            UnaryExpression { NodeType: ExpressionType.Quote } unaryExpression => unaryExpression.Operand,
            _ => throw new NotSupportedException($"the analyzer planned a subscription to {expression}, whose value the execution path cannot resolve without invoking something")
        };

    private protected DirectSubscriptionAttachment[] attachments = [];
    private protected int released;

    internal override bool CanChange =>
        attachments.Length > 0;

    protected override bool DisposeCore()
    {
        if (Interlocked.Exchange(ref released, 1) != 0)
            return false;
        var attached = attachments;
        for (var i = 0; i < attached.Length; ++i)
            observer.DirectSubscriptions.Detach(attached[i]);
        RemovedFromCache();
        return true;
    }

    private protected void Attach(DirectSubscriptionSite[] sites, object? argument, object?[] values)
    {
        if (sites.Length == 0)
            return;
        var attaching = new DirectSubscriptionAttachment[sites.Length];
        var attached = 0;
        for (var i = 0; i < sites.Length; ++i)
        {
            var site = sites[i];
            if (site.DeferredGroup != 0)
                continue;
            var source = site.ResolveSource(argument, values);
            if (source is null || site.ResolveKind(source) is var kind && kind is DirectSubscriptionKind.None)
                continue;
            attaching[attached++] = observer.DirectSubscriptions.Attach(source, kind, site.PropertyName, this, site.ForcesNotification);
        }
        if (attached > 0)
            attachments = attached == sites.Length ? attaching : attaching[..attached];
    }

    /// <summary>
    /// Attaches the subscriptions of a deferred group, which is done the first time the operand of that group is evaluated because that is when the graph attaches the nodes of that operand
    /// </summary>
    private protected bool AttachDeferred(DirectSubscriptionSite[] sites, int group, object? argument, object?[] values)
    {
        List<DirectSubscriptionAttachment>? added = null;
        for (var i = 0; i < sites.Length; ++i)
        {
            var site = sites[i];
            if (site.DeferredGroup != group)
                continue;
            var source = site.ResolveSource(argument, values);
            if (source is null || site.ResolveKind(source) is var kind && kind is DirectSubscriptionKind.None)
                continue;
            (added ??= []).Add(observer.DirectSubscriptions.Attach(source, kind, site.PropertyName, this, site.ForcesNotification));
        }
        if (added is null)
            return false;
        while (true)
        {
            var current = attachments;
            var updated = new DirectSubscriptionAttachment[current.Length + added.Count];
            Array.Copy(current, updated, current.Length);
            for (var i = 0; i < added.Count; ++i)
                updated[current.Length + i] = added[i];
            if (ReferenceEquals(Interlocked.CompareExchange(ref attachments, updated, current), current))
                break;
        }
        if (Volatile.Read(ref released) != 0)
            for (var i = 0; i < added.Count; ++i)
                observer.DirectSubscriptions.Detach(added[i]);
        return true;
    }

    internal void OnSourceChanged(bool forcesNotification)
    {
        Evaluate();
        if (forcesNotification)
            NotifyDependentsOfValueContentsChanged();
    }
}

class DirectObservableExpression<TArgument, TResult> :
    DirectObservableExpression
{
    static readonly bool sharesBooleanBoxes = typeof(TResult) == typeof(bool);
    static readonly TResult trueResult = typeof(TResult) == typeof(bool) ? (TResult)(object)true : default!;

    internal DirectObservableExpression(ExpressionObserver observer, Expression<Func<TArgument, TResult>> lambdaExpression, DirectSubscriptionSite[] sites, Func<TArgument, object?[], bool[], TResult> evaluate, TArgument argument, object?[] values, bool[] reached) :
        base(observer, lambdaExpression.Body.Type)
    {
        this.argument = argument;
        comparesBeforeBoxing = typeof(TResult).IsValueType && lambdaExpression.Body.Type == typeof(TResult);
        this.evaluate = evaluate;
        this.lambdaExpression = lambdaExpression;
        this.reached = reached;
        this.sites = sites;
        this.values = values;
    }

    private protected readonly TArgument argument;
    readonly bool comparesBeforeBoxing;
    private protected readonly Func<TArgument, object?[], bool[], TResult> evaluate;
    readonly Expression<Func<TArgument, TResult>> lambdaExpression;
    private protected readonly bool[] reached;
    private protected readonly DirectSubscriptionSite[] sites;
    private protected readonly object?[] values;

    private protected override Expression Materialize() =>
        ExpressionObserver.ReplaceParametersWithoutOptimization(lambdaExpression, argument)!;

    private protected static object? Box(TResult value) =>
        sharesBooleanBoxes ? BooleanBoxes.Box(EqualityComparer<TResult>.Default.Equals(value, trueResult)) : value;

    protected override void Evaluate()
    {
        try
        {
            var value = evaluate(argument, values, reached);
            if (!IsCurrentResult(value))
                Evaluation = (null, Box(value));
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{Expression} evaluated directly: {Value}", Expression, value);
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{Expression} faulted: {Fault}", Expression, ex);
        }
    }

    private protected bool IsCurrentResult(TResult value)
    {
        if (!comparesBeforeBoxing)
            return false;
        var current = CurrentEvaluation;
        return current.Fault is null && current.Result is TResult result && EqualityComparer<TResult>.Default.Equals(result, value);
    }

    protected override void OnInitialization()
    {
        try
        {
            Attach(sites, argument, values);
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeCore();
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}

/// <summary>
/// An observation of an expression which defers the evaluation of one or more of its operands, and which therefore attaches the subscriptions of an operand only once an evaluation has reached it
/// </summary>
/// <remarks>
/// The evaluation is repeated after a group is attached so that the result kept is one read after the subscription was in place, which is the order the graph's nodes observe
/// </remarks>
sealed class DeferringDirectObservableExpression<TArgument, TResult> :
    DirectObservableExpression<TArgument, TResult>
{
    internal DeferringDirectObservableExpression(ExpressionObserver observer, Expression<Func<TArgument, TResult>> lambdaExpression, DirectSubscriptionSite[] sites, Func<TArgument, object?[], bool[], TResult> evaluate, TArgument argument, object?[] values, bool[] reached) :
        base(observer, lambdaExpression, sites, evaluate, argument, values, reached)
    {
    }

    long attachedGroups;

    internal override bool CanChange =>
        true;

    bool AttachNewlyReached()
    {
        var attached = false;
        for (var group = 0; group < reached.Length; ++group)
        {
            if (!reached[group])
                continue;
            var bit = 1L << group;
            var current = Interlocked.Read(ref attachedGroups);
            while ((current & bit) == 0)
            {
                var exchanged = Interlocked.CompareExchange(ref attachedGroups, current | bit, current);
                if (exchanged == current)
                {
                    attached |= AttachDeferred(sites, group + 1, argument, values);
                    break;
                }
                current = exchanged;
            }
        }
        return attached;
    }

    protected override void Evaluate()
    {
        while (true)
        {
            try
            {
                var value = evaluate(argument, values, reached);
                if (AttachNewlyReached())
                    continue;
                if (!IsCurrentResult(value))
                    Evaluation = (null, Box(value));
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{Expression} evaluated directly: {Value}", Expression, value);
                return;
            }
            catch (Exception ex)
            {
                if (AttachNewlyReached())
                    continue;
                Evaluation = (ex, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{Expression} faulted: {Fault}", Expression, ex);
                return;
            }
        }
    }
}
