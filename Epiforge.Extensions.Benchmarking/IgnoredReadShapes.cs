namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// A read of a property whose change notifications are ignored, made through something which can move, beside a property which is followed, and the observers each arm observes it with
/// </summary>
static class IgnoredReadShapes
{
    static readonly PropertyInfo rankProperty = typeof(BenchmarkHolding).GetProperty(nameof(BenchmarkHolding.Rank))!;

    internal static readonly Expression<Func<BenchmarkHolder, int>> Reading = h => h.Holding.Rank + h.Score;

    /// <summary>
    /// The default mechanism told to ignore the property, which today refuses the shape and sends it to the graph
    /// </summary>
    internal static ExpressionObserver IgnoringDefault()
    {
        var options = new ExpressionObserverOptions();
        options.AddIgnoredPropertyChangeNotification(rankProperty);
        return new(options);
    }

    internal static ExpressionObserver IgnoringGraph()
    {
        var options = new ExpressionObserverOptions { UseDirectSubscription = false };
        options.AddIgnoredPropertyChangeNotification(rankProperty);
        return new(options);
    }

    /// <summary>
    /// The default mechanism told nothing, which serves the shape on the fast path and follows the property, so that it prices what ignoring it was meant to save
    /// </summary>
    internal static ExpressionObserver FollowingDefault() =>
        new();
}
