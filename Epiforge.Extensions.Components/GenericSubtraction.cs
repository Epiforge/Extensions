namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides a delegate that subtracts one value from another
/// </summary>
/// <typeparam name="TMinuend">The type of the minuend</typeparam>
/// <typeparam name="TSubtrahend">The type of the subtrahend</typeparam>
/// <typeparam name="TDifference">The type of the difference</typeparam>
[SuppressMessage("Design", "CA1005: Avoid excessive parameters on generic types")]
public static class GenericSubtraction<TMinuend, TSubtrahend, TDifference>
{
    static readonly Lazy<Func<TMinuend, TSubtrahend, TDifference>> lazyFunc = new(() =>
    {
        var minuend = Expression.Parameter(typeof(TMinuend), "minuend");
        var subtrahend = Expression.Parameter(typeof(TSubtrahend), "subtrahend");
        return Expression.Lambda<Func<TMinuend, TSubtrahend, TDifference>>(Expression.Subtract(minuend, subtrahend), minuend, subtrahend).Compile();
    }, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Gets the instance
    /// </summary>
    public static Func<TMinuend, TSubtrahend, TDifference> Instance =>
        lazyFunc.Value;
}