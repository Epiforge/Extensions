namespace Epiforge.Extensions.Expressions.Tests.Observable;

public static class ExpressionObserverHelpers
{
    /// <summary>
    /// Creates an observer which observes by the mechanism named rather than by whichever one happens to be the default
    /// </summary>
    /// <remarks>
    /// A test which does not say which mechanism it wants exercises the default and leaves the other one untested. For the expression node tests that meant fourteen of fifteen classes exercising the fast path rather than the graph node each is named for, which is why they now take this as a row
    /// </remarks>
    public static ExpressionObserver Create(bool useDirectSubscription, ExpressionObserverOptions? options = null)
    {
        options ??= new ExpressionObserverOptions();
        options.UseDirectSubscription = useDirectSubscription;
        return Create(options);
    }

    public static ExpressionObserver Create(ExpressionObserverOptions? options = null)
    {
        options ??= new ExpressionObserverOptions();
        options.Optimizer = ExpressionOptimizer.tryVisit;
        return new(options);
    }
}
