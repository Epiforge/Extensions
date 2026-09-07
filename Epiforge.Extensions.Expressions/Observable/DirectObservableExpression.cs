namespace Epiforge.Extensions.Expressions.Observable;

abstract class DirectObservableExpression(ExpressionObserver observer, Type type) :
    ObservableExpression(observer, type, false)
{
    private protected static readonly bool[] noDeferredGroups = [];
    private protected static readonly object?[] noLinks = [];

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

    /// <summary>
    /// Counts the sites of a group which resolve to something to attach to, which is done before anything is allocated so that the array of attachments is made once at the size it will keep
    /// </summary>
    static int Attaching(DirectSubscriptionSite[] sites, int group, object? argument, object?[] values, object?[] links)
    {
        var attaching = 0;
        for (var i = 0; i < sites.Length; ++i)
        {
            var site = sites[i];
            if (site.DeferredGroup != group || site.Link >= 0)
                continue;
            var source = site.ResolveSource(argument, values, links);
            if (source is not null && site.ResolveKind(source) is not DirectSubscriptionKind.None)
                ++attaching;
        }
        return attaching;
    }

    /// <summary>
    /// Attaches one site to whatever its source resolves to now, which is <c>null</c> where that value notifies of nothing the site wants
    /// </summary>
    private protected DirectSubscriptionAttachment? AttachSite(DirectSubscriptionSite site, object? argument, object?[] values, object?[] links)
    {
        var source = site.ResolveSource(argument, values, links);
        return source is null || site.ResolveKind(source) is var kind && kind is DirectSubscriptionKind.None ? null : observer.DirectSubscriptions.Attach(source, kind, site.PropertyName, this, site.ForcesNotification);
    }

    private protected void Attach(DirectSubscriptionSite[] sites, object? argument, object?[] values, object?[] links)
    {
        if (Attaching(sites, 0, argument, values, links) is var attaching && attaching == 0)
            return;
        var attached = new DirectSubscriptionAttachment[attaching];
        var index = 0;
        for (var i = 0; i < sites.Length; ++i)
        {
            var site = sites[i];
            if (site.DeferredGroup != 0 || site.Link >= 0)
                continue;
            if (AttachSite(site, argument, values, links) is { } attachment)
                attached[index++] = attachment;
        }
        attachments = attached;
    }

    /// <summary>
    /// Attaches the subscriptions of a deferred group, which is done the first time the operand of that group is evaluated because that is when the graph attaches the nodes of that operand
    /// </summary>
    private protected bool AttachDeferred(DirectSubscriptionSite[] sites, int group, object? argument, object?[] values, object?[] links)
    {
        if (Attaching(sites, group, argument, values, links) is var attaching && attaching == 0)
            return false;
        var current = attachments;
        var updated = new DirectSubscriptionAttachment[current.Length + attaching];
        Array.Copy(current, updated, current.Length);
        var index = current.Length;
        for (var i = 0; i < sites.Length; ++i)
        {
            var site = sites[i];
            if (site.DeferredGroup != group || site.Link >= 0)
                continue;
            if (AttachSite(site, argument, values, links) is { } attachment)
                updated[index++] = attachment;
        }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref attachments, updated, current), current))
        {
            var latest = attachments;
            var merged = new DirectSubscriptionAttachment[latest.Length + attaching];
            Array.Copy(latest, merged, latest.Length);
            Array.Copy(updated, updated.Length - attaching, merged, latest.Length, attaching);
            current = latest;
            updated = merged;
        }
        if (Volatile.Read(ref released) != 0)
            for (var i = updated.Length - attaching; i < updated.Length; ++i)
                observer.DirectSubscriptions.Detach(updated[i]);
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

    internal DirectObservableExpression(ExpressionObserver observer, Expression<Func<TArgument, TResult>> lambdaExpression, DirectSubscriptionSite[] sites, Func<TArgument, object?[], bool[], object?[], TResult> evaluate, TArgument argument, object?[] values) :
        base(observer, lambdaExpression.Body.Type)
    {
        this.argument = argument;
        comparesBeforeBoxing = typeof(TResult).IsValueType && lambdaExpression.Body.Type == typeof(TResult);
        this.evaluate = evaluate;
        this.lambdaExpression = lambdaExpression;
        this.sites = sites;
        this.values = values;
    }

    private protected readonly TArgument argument;
    readonly bool comparesBeforeBoxing;
    private protected readonly Func<TArgument, object?[], bool[], object?[], TResult> evaluate;
    readonly Expression<Func<TArgument, TResult>> lambdaExpression;
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
            var value = evaluate(argument, values, noDeferredGroups, noLinks);
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
            Attach(sites, argument, values, noLinks);
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
    internal DeferringDirectObservableExpression(ExpressionObserver observer, Expression<Func<TArgument, TResult>> lambdaExpression, DirectSubscriptionSite[] sites, Func<TArgument, object?[], bool[], object?[], TResult> evaluate, TArgument argument, object?[] values, bool[] reached, object?[] links, int[] linkSites) :
        base(observer, lambdaExpression, sites, evaluate, argument, values)
    {
        attachedLinks = links.Length == 0 ? links : new object?[links.Length];
        linkAttachments = linkSites.Length == 0 ? [] : new DirectSubscriptionAttachment?[linkSites.Length];
        this.links = links;
        this.linkSites = linkSites;
        this.reached = reached;
    }

    long attachedGroups;
    readonly object?[] attachedLinks;
    readonly DirectSubscriptionAttachment?[] linkAttachments;
    readonly object?[] links;
    readonly int[] linkSites;
    readonly bool[] reached;

    internal override bool CanChange =>
        true;

    /// <summary>
    /// Moves the subscriptions of every link whose value is not the one they are attached to, which is what the graph's node does when the value it read last is not the value it reads now
    /// </summary>
    bool AttachChangedLinks()
    {
        var moved = false;
        for (var i = 0; i < links.Length; ++i)
        {
            var current = links[i];
            var attached = attachedLinks[i];
            if (ReferenceEquals(current, attached) || !ReferenceEquals(Interlocked.CompareExchange(ref attachedLinks[i], current, attached), attached))
                continue;
            for (var s = 0; s < linkSites.Length; ++s)
            {
                var site = sites[linkSites[s]];
                if (site.Link != i)
                    continue;
                if (Interlocked.Exchange(ref linkAttachments[s], null) is { } previous)
                    observer.DirectSubscriptions.Detach(previous);
                if (current is not null && AttachSite(site, argument, values, links) is { } attachment)
                {
                    linkAttachments[s] = attachment;
                    if (Volatile.Read(ref released) != 0 && Interlocked.Exchange(ref linkAttachments[s], null) is { } releasing)
                        observer.DirectSubscriptions.Detach(releasing);
                }
                moved = true;
            }
        }
        return moved;
    }

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
                    attached |= AttachDeferred(sites, group + 1, argument, values, links);
                    break;
                }
                current = exchanged;
            }
        }
        return attached;
    }

    protected override bool DisposeCore()
    {
        if (!base.DisposeCore())
            return false;
        for (var s = 0; s < linkAttachments.Length; ++s)
            if (Interlocked.Exchange(ref linkAttachments[s], null) is { } attachment)
                observer.DirectSubscriptions.Detach(attachment);
        return true;
    }

    protected override void Evaluate()
    {
        var moved = false;
        while (true)
        {
            try
            {
                var value = evaluate(argument, values, reached, links);
                var again = AttachNewlyReached();
                if (!moved && AttachChangedLinks())
                    again = moved = true;
                if (again)
                    continue;
                if (!IsCurrentResult(value))
                    Evaluation = (null, Box(value));
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{Expression} evaluated directly: {Value}", Expression, value);
                return;
            }
            catch (Exception ex)
            {
                var again = AttachNewlyReached();
                if (!moved && AttachChangedLinks())
                    again = moved = true;
                if (again)
                    continue;
                Evaluation = (ex, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{Expression} faulted: {Fault}", Expression, ex);
                return;
            }
        }
    }
}
