namespace Epiforge.Extensions.Expressions.Observable;

abstract class DirectObservableExpression :
    ObservableExpression
{
    protected static object? Resolve(Expression expression) =>
        expression switch
        {
            ConstantExpression constantExpression => constantExpression.Value,
            MemberExpression { Member: FieldInfo field } memberExpression => field.GetValue(memberExpression.Expression is { } target ? Resolve(target) : null),
            UnaryExpression { NodeType: ExpressionType.Quote } unaryExpression => unaryExpression.Operand,
            _ => throw new NotSupportedException($"the analyzer planned a subscription to {expression}, whose value the execution path cannot resolve without invoking something")
        };

    protected DirectObservableExpression(ExpressionObserver observer, Expression expression) :
        base(observer, expression, false)
    {
    }

    DirectSubscriptionAttachment[]? attachments;
    int released;

    internal override bool CanChange =>
        attachments is { Length: > 0 };

    protected override bool DisposeCore()
    {
        if (Interlocked.Exchange(ref released, 1) != 0)
            return false;
        if (attachments is { } attached)
            for (var i = 0; i < attached.Length; ++i)
                observer.DirectSubscriptions.Detach(attached[i]);
        RemovedFromCache();
        return true;
    }

    private protected void Attach(DirectSubscriptionPlan plan)
    {
        var subscriptions = plan.Subscriptions;
        var attaching = new List<DirectSubscriptionAttachment>(subscriptions.Count);
        for (int i = 0, ii = subscriptions.Count; i < ii; ++i)
        {
            var subscription = subscriptions[i];
            var source = Resolve(subscription.Source!);
            if (source is null || subscription.ResolveKind(source) is var kind && kind is DirectSubscriptionKind.None)
                continue;
            attaching.Add(observer.DirectSubscriptions.Attach(source, kind, subscription.PropertyName, this, ReferenceEquals(subscription.Source, Expression)));
        }
        attachments = [.. attaching];
    }

    internal void OnSourceChanged(bool forcesNotification)
    {
        Evaluate();
        if (forcesNotification)
            NotifyDependentsOfValueContentsChanged();
    }
}

sealed class DirectObservableExpression<TArgument, TResult> :
    DirectObservableExpression
{
    internal DirectObservableExpression(ExpressionObserver observer, Expression expression, DirectSubscriptionPlan plan, Func<TArgument, TResult> evaluate, TArgument argument) :
        base(observer, expression)
    {
        this.argument = argument;
        this.evaluate = evaluate;
        this.plan = plan;
    }

    readonly TArgument argument;
    readonly Func<TArgument, TResult> evaluate;
    readonly DirectSubscriptionPlan plan;

    protected override void Evaluate()
    {
        try
        {
            var value = evaluate(argument);
            Evaluation = (null, value);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{Expression} evaluated directly: {Value}", Expression, value);
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{Expression} faulted: {Fault}", Expression, ex);
        }
    }

    protected override void OnInitialization()
    {
        try
        {
            Attach(plan);
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            DisposeCore();
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
