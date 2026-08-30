namespace Epiforge.Extensions.Expressions.Observable;

abstract class DirectObservableExpression :
    ObservableExpression
{
    internal static object? Resolve(Expression expression) =>
        expression switch
        {
            ConstantExpression constantExpression => constantExpression.Value,
            MemberExpression { Member: FieldInfo field } memberExpression => field.GetValue(memberExpression.Expression is { } target ? Resolve(target) : null),
            UnaryExpression { NodeType: ExpressionType.Quote } unaryExpression => unaryExpression.Operand,
            _ => throw new NotSupportedException($"the analyzer planned a subscription to {expression}, whose value the execution path cannot resolve without invoking something")
        };

    protected DirectObservableExpression(ExpressionObserver observer, Type type) :
        base(observer, type, false)
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

    private protected void Attach(DirectSubscriptionSite[] sites, object? argument, object?[] values)
    {
        var attaching = new DirectSubscriptionAttachment[sites.Length];
        var attached = 0;
        for (var i = 0; i < sites.Length; ++i)
        {
            var site = sites[i];
            var source = site.ResolveSource(argument, values);
            if (source is null || site.ResolveKind(source) is var kind && kind is DirectSubscriptionKind.None)
                continue;
            attaching[attached++] = observer.DirectSubscriptions.Attach(source, kind, site.PropertyName, this, site.ForcesNotification);
        }
        attachments = attached == sites.Length ? attaching : attaching[..attached];
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
    internal DirectObservableExpression(ExpressionObserver observer, Expression<Func<TArgument, TResult>> lambdaExpression, DirectSubscriptionSite[] sites, Func<TArgument, object?[], TResult> evaluate, TArgument argument, object?[] values) :
        base(observer, lambdaExpression.Body.Type)
    {
        this.argument = argument;
        this.evaluate = evaluate;
        this.lambdaExpression = lambdaExpression;
        this.sites = sites;
        this.values = values;
    }

    readonly TArgument argument;
    readonly Func<TArgument, object?[], TResult> evaluate;
    readonly Expression<Func<TArgument, TResult>> lambdaExpression;
    readonly DirectSubscriptionSite[] sites;
    readonly object?[] values;

    private protected override Expression Materialize() =>
        ExpressionObserver.ReplaceParametersWithoutOptimization(lambdaExpression, argument)!;

    protected override void Evaluate()
    {
        try
        {
            var value = evaluate(argument, values);
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
