namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides a delegate that multiplies one value by another
/// </summary>
/// <typeparam name="TMultiplicand">The type of the multiplicand</typeparam>
/// <typeparam name="TMultiplier">The type of the multiplier</typeparam>
/// <typeparam name="TProduct">The type of the product</typeparam>
[SuppressMessage("Design", "CA1005: Avoid excessive parameters on generic types")]
public static class GenericMultiplication<TMultiplicand, TMultiplier, TProduct>
{
    static readonly Lazy<Func<TMultiplicand, TMultiplier, TProduct>> lazyFunc = new(() =>
    {
        var multiplicand = Expression.Parameter(typeof(TMultiplicand), "multiplicand");
        var multiplier = Expression.Parameter(typeof(TMultiplier), "multiplier");
        return Expression.Lambda<Func<TMultiplicand, TMultiplier, TProduct>>(Expression.Multiply(multiplicand, multiplier), multiplicand, multiplier).Compile();
    }, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Gets the instance
    /// </summary>
    public static Func<TMultiplicand, TMultiplier, TProduct> Instance =>
        lazyFunc.Value;
}