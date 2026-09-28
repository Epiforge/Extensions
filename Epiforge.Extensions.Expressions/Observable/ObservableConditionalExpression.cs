namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableConditionalExpression(ExpressionObserver observer, ConditionalExpression conditionalExpression, bool deferEvaluation) :
    ObservableExpression(observer, conditionalExpression, deferEvaluation),
    IObservableExpressionDependent
{
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? ifFalse;
    ObservableExpressionSubscription? ifFalseSubscription;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? ifTrue;
    ObservableExpressionSubscription? ifTrueSubscription;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? test;
    ObservableExpressionSubscription? testSubscription;

    internal readonly ConditionalExpression ConditionalExpression = conditionalExpression;

    protected override bool DisposeCore()
    {
        var removedFromCache = observer.ExpressionDisposed(this);
        if (removedFromCache)
            Retire();
        return removedFromCache;
    }

    private protected override void TearDown()
    {
        if (test is not null)
        {
            if (testSubscription is { } testDependency)
                test.UnsubscribeDependent(testDependency);
            test.Release();
        }
        if (ifTrue is not null)
        {
            if (ifTrueSubscription is { } ifTrueDependency)
                ifTrue.UnsubscribeDependent(ifTrueDependency);
            ifTrue.Release();
        }
        if (ifFalse is not null)
        {
            if (ifFalseSubscription is { } ifFalseDependency)
                ifFalse.UnsubscribeDependent(ifFalseDependency);
            ifFalse.Release();
        }
        RemovedFromCache();
    }

    protected override void Evaluate()
    {
        var (testFault, testResult) = test?.Evaluation ?? (null, null);
        if (testFault is not null)
        {
            Evaluation = (testFault, defaultResult);
            observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted, testFault, "{ConditionalExpression} test faulted: {Fault}", ConditionalExpression, testFault);
        }
        else if (testResult is bool testBool)
        {
            Evaluation = testBool ? ifTrue!.Evaluation : ifFalse!.Evaluation;
            observer.TraceLogger?.LogTrace(EventIds.Epiforge_Extensions_Expressions_ExpressionEvaluated, "{ConditionalExpression} test: {TestResult}", ConditionalExpression, testBool);
        }
        else
        {
            Evaluation = (new InvalidCastException(), defaultResult);
            observer.TraceLogger?.LogWarning(EventIds.Epiforge_Extensions_Expressions_ConditionalExpressionTestInvalidType, "{ConditionalExpression} test is of type {TestResultType} when a boolean is required", ConditionalExpression, testResult?.GetType());
        }
    }

    protected override void OnInitialization()
    {
        try
        {
            var conditionalExpression = ConditionalExpression;
            test = observer.GetObservableExpression(conditionalExpression.Test, IsDeferringEvaluation);
            ifTrue = observer.GetObservableExpression(conditionalExpression.IfTrue, true);
            ifFalse = observer.GetObservableExpression(conditionalExpression.IfFalse, true);
            if (test.CanChange)
                testSubscription = test.SubscribeDependent(this);
            if (ifTrue.CanChange)
                ifTrueSubscription = ifTrue.SubscribeDependent(this);
            if (ifFalse.CanChange)
                ifFalseSubscription = ifFalse.SubscribeDependent(this);
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            if (test is not null)
            {
                if (testSubscription is { } testDependency)
                    test.UnsubscribeDependent(testDependency);
                test.Release();
            }
            if (ifTrue is not null)
            {
                if (ifTrueSubscription is { } ifTrueDependency)
                    ifTrue.UnsubscribeDependent(ifTrueDependency);
                ifTrue.Release();
            }
            if (ifFalse is not null)
            {
                if (ifFalseSubscription is { } ifFalseDependency)
                    ifFalse.UnsubscribeDependent(ifFalseDependency);
                ifFalse.Release();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void IObservableExpressionDependent.OnDependencyEvaluationChanged(ObservableExpression dependency) =>
        EvaluateOnce();
}
