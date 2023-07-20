namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

public static class CollectionObserverHelpers
{
    public static CollectionObserver Create() =>
        new(ExpressionObserverHelpers.Create());
}
