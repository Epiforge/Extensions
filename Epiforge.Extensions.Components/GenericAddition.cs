namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides a delegate that adds two values
/// </summary>
/// <typeparam name="TLeftAddend">The type of the left addend</typeparam>
/// <typeparam name="TRightAddend">The type of the right addend</typeparam>
/// <typeparam name="TSum">The type of the sum</typeparam>
[SuppressMessage("Design", "CA1005: Avoid excessive parameters on generic types")]
public static class GenericAddition<TLeftAddend, TRightAddend, TSum>
{
    static readonly Lazy<Func<TLeftAddend, TRightAddend, TSum>> lazyFunc = new(() =>
    {
        var leftAddend = Expression.Parameter(typeof(TLeftAddend), "leftAddend");
        var rightAddend = Expression.Parameter(typeof(TRightAddend), "rightAddend");
        return Expression.Lambda<Func<TLeftAddend, TRightAddend, TSum>>(Expression.Add(leftAddend, rightAddend), leftAddend, rightAddend).Compile();
    }, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Gets the delegate
    /// </summary>
    public static Func<TLeftAddend, TRightAddend, TSum> Instance =>
        lazyFunc.Value;
}
