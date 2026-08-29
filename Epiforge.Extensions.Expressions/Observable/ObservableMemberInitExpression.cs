namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableMemberInitExpression(ExpressionObserver observer, MemberInitExpression memberInitExpression, bool deferEvaluation) :
    ObservableExpression(observer, memberInitExpression, deferEvaluation),
    IObservableExpressionDependent
{
    IReadOnlyDictionary<ObservableExpression, (MemberInfo Member, ObservableExpressionSubscription? Subscription)>? memberAssignmentObservableExpressions;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? newObservableExpression;
    ObservableExpressionSubscription? newObservableExpressionSubscription;

    internal readonly MemberInitExpression MemberInitExpression = memberInitExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.ExpressionDisposed(this);
            if (removedFromCache)
            {
                DisposeValueIfNecessaryAndPossible();
                if (newObservableExpression is not null)
                {
                    if (newObservableExpressionSubscription is { } newObservableExpressionDependency)
                        newObservableExpression.UnsubscribeDependent(newObservableExpressionDependency);
                    newObservableExpression.Dispose();
                }
                if (memberAssignmentObservableExpressions is not null)
                    foreach (var kv in memberAssignmentObservableExpressions)
                    {
                        if (kv.Value.Subscription is { } memberAssignmentDependency)
                            kv.Key.UnsubscribeDependent(memberAssignmentDependency);
                        kv.Key.Dispose();
                    }
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void Evaluate()
    {
        try
        {
            var (newObservableExpressionFault, newObservableExpressionResult) = newObservableExpression?.Evaluation ?? (null, null);
            if (newObservableExpressionFault is not null)
            {
                Evaluation = (newObservableExpressionFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, newObservableExpressionFault, "{MemberInitExpression} new faulted: {Fault}", MemberInitExpression, newObservableExpressionFault);
            }
            else if (memberAssignmentObservableExpressions?.Keys.Select(memberAssignmentObservableExpression => memberAssignmentObservableExpression.Evaluation.Fault).FirstOrDefault(fault => fault is not null) is { } memberAssignmentObservableExpressionFault)
            {
                Evaluation = (memberAssignmentObservableExpressionFault, defaultResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, memberAssignmentObservableExpressionFault, "{MemberInitExpression} member assignment faulted: {Fault}", MemberInitExpression, memberAssignmentObservableExpressionFault);
            }
            else
            {
                if (memberAssignmentObservableExpressions is not null)
                    foreach (var kv in memberAssignmentObservableExpressions)
                    {
                        if (kv.Value.Member is FieldInfo field)
                            field.SetValue(newObservableExpressionResult, kv.Key.Evaluation.Result);
                        else if (kv.Value.Member is PropertyInfo property)
                            property.FastSetValue(newObservableExpressionResult, kv.Key.Evaluation.Result);
                        else
                            throw new NotSupportedException("Cannot handle member that is not a field or property");
                    }
                Evaluation = (null, newObservableExpressionResult);
                observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{MemberInitExpression} evaluated: {Value}", MemberInitExpression, newObservableExpressionResult);
            }
        }
        catch (Exception ex)
        {
            Evaluation = (ex, defaultResult);
            observer.Logger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, ex, "{MemberInitExpression} faulted: {Fault}", MemberInitExpression, ex);
        }
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency)
    {
        if (ReferenceEquals(dependency, newObservableExpression))
        {
            Evaluate();
            return;
        }
        var memberAssignmentObservableExpression = dependency;
        if (memberAssignmentObservableExpressions?.TryGetValue(memberAssignmentObservableExpression, out var assignment) ?? false)
        {
            var member = assignment.Member;
            var (memberAssignmentObservableExpressionFault, memberAssignmentObservableExpressionResult) = memberAssignmentObservableExpression.Evaluation;
            if (memberAssignmentObservableExpressionFault is not null)
                Evaluation = (memberAssignmentObservableExpressionFault, defaultResult);
            else
            {
                var intactResult = TryGetUndeferredResult(out var result) && result is not null;
                if (!intactResult)
                    result = newObservableExpression?.Evaluation.Result;
                if (result is not null)
                {
                    if (member is FieldInfo field)
                        field.SetValue(result, memberAssignmentObservableExpressionResult);
                    else if (member is PropertyInfo property)
                        property.FastSetValue(result, memberAssignmentObservableExpressionResult);
                    else
                        throw new NotSupportedException("Cannot handle member that is not a field or property");
                }
                if (!intactResult)
                    Evaluation = (null, result);
            }
        }
    }

    protected override void OnInitialization()
    {
        if (MemberInitExpression.NewExpression.Type.IsValueType)
            throw new NotSupportedException("Member initialization expressions of value types are not supported");
        var memberAssignmentObservableExpressions = new Dictionary<ObservableExpression, (MemberInfo Member, ObservableExpressionSubscription? Subscription)>(ObservableExpressionEqualityComparer.Default);
        try
        {
            newObservableExpression = observer.GetObservableExpression(MemberInitExpression.NewExpression, IsDeferringEvaluation);
            if (newObservableExpression.CanChange)
                newObservableExpressionSubscription = newObservableExpression.SubscribeDependent(this);
            var bindings = MemberInitExpression.Bindings;
            for (int i = 0, ii = bindings.Count; i < ii; ++i)
            {
                var binding = bindings[i];
                if (binding is MemberAssignment memberAssignmentBinding)
                {
                    var memberAssignmentObservableExpression = observer.GetObservableExpression(memberAssignmentBinding.Expression, IsDeferringEvaluation);
                    memberAssignmentObservableExpressions.Add(memberAssignmentObservableExpression, (memberAssignmentBinding.Member, memberAssignmentObservableExpression.CanChange ? memberAssignmentObservableExpression.SubscribeDependent(this) : null));
                }
                else
                    throw new NotSupportedException("Only member assignment bindings are supported in member init expressions");
            }
            this.memberAssignmentObservableExpressions = memberAssignmentObservableExpressions;
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            if (newObservableExpression is not null)
            {
                if (newObservableExpressionSubscription is { } newObservableExpressionDependency)
                    newObservableExpression.UnsubscribeDependent(newObservableExpressionDependency);
                newObservableExpression.Dispose();
            }
            foreach (var kv in memberAssignmentObservableExpressions)
            {
                if (kv.Value.Subscription is { } memberAssignmentDependency)
                    kv.Key.UnsubscribeDependent(memberAssignmentDependency);
                kv.Key.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
