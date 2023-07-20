namespace Epiforge.Extensions.Expressions.Tests.Observable;

public static class ExpressionObserverHelpers
{
    public static ExpressionObserver Create(ExpressionObserverOptions? options = null)
    {
        options ??= new ExpressionObserverOptions();
#if IS_NET_STANDARD_2_1_OR_GREATER
        options.Optimizer = ExpressionOptimizer.tryVisit;
#endif
        return new(options);
    }
}
