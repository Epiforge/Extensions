namespace Epiforge.Extensions.Expressions.Observable;

sealed class ObservableConditionalExpression :
    ObservableExpression
{
    public ObservableConditionalExpression(ExpressionObserver observer, ConditionalExpression conditionalExpression, bool deferEvaluation) :
        base(observer, conditionalExpression, deferEvaluation) =>
        ConditionalExpression = conditionalExpression;

    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? ifFalse;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? ifTrue;
    [SuppressMessage("Usage", "CA2213: Disposable fields should be disposed")]
    ObservableExpression? test;

    internal readonly ConditionalExpression ConditionalExpression;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = observer.Disposed(this);
            if (test is not null)
            {
                test.PropertyChanged -= TestPropertyChanged;
                test.Dispose();
            }
            if (ifTrue is not null)
            {
                ifTrue.PropertyChanged -= IfTruePropertyChanged;
                ifTrue.Dispose();
            }
            if (ifFalse is not null)
            {
                ifFalse.PropertyChanged -= IfFalsePropertyChanged;
                ifFalse.Dispose();
            }
            return removedFromCache;
        }
        return true;
    }

    protected override void Evaluate()
    {
        var (testFault, testResult) = test?.Evaluation ?? (null, null);
        if (testFault is not null)
            Evaluation = (testFault, defaultResult);
        else
            Evaluation = testResult is bool testBool && testBool ? ifTrue!.Evaluation : ifFalse!.Evaluation;
    }

    protected override void OnInitialization()
    {
        try
        {
            var conditionalExpression = ConditionalExpression;
            test = observer.GetObservableExpression(conditionalExpression.Test, IsDeferringEvaluation);
            test.PropertyChanged += TestPropertyChanged;
            ifTrue = observer.GetObservableExpression(conditionalExpression.IfTrue, true);
            ifTrue.PropertyChanged += IfTruePropertyChanged;
            ifFalse = observer.GetObservableExpression(conditionalExpression.IfFalse, true);
            ifFalse.PropertyChanged += IfFalsePropertyChanged;
            EvaluateIfNotDeferred();
        }
        catch (Exception ex)
        {
            if (test is not null)
            {
                test.PropertyChanged -= TestPropertyChanged;
                test.Dispose();
            }
            if (ifTrue is not null)
            {
                ifTrue.PropertyChanged -= IfTruePropertyChanged;
                ifTrue.Dispose();
            }
            if (ifFalse is not null)
            {
                ifFalse.PropertyChanged -= IfFalsePropertyChanged;
                ifFalse.Dispose();
            }
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    void IfFalsePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var (testFault, testResult) = test?.Evaluation ?? (null, null);
        if (testFault is null && !(testResult is bool testBool && testBool))
            Evaluation = ifFalse!.Evaluation;
    }

    void IfTruePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var (testFault, testResult) = test?.Evaluation ?? (null, null);
        if (testFault is null && testResult is bool testBool && testBool)
            Evaluation = ifTrue!.Evaluation;
    }

    void TestPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Evaluate();
}
