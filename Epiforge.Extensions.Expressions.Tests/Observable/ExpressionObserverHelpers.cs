namespace Epiforge.Extensions.Expressions.Tests.Observable;

public static class ExpressionObserverHelpers
{
    public static ExpressionObserver Create(ExpressionObserverOptions? options = null)
    {
        options ??= new ExpressionObserverOptions();
        options.Optimizer = ExpressionOptimizer.tryVisit;
        return new(options);
    }
}
