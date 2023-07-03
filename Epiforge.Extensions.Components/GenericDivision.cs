namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides a delegate that divides one value by another
/// </summary>
/// <typeparam name="TDividend">The type of the dividend</typeparam>
/// <typeparam name="TDivisor">The type of the divisor</typeparam>
/// <typeparam name="TQuotient">The type of the quotient</typeparam>
[SuppressMessage("Design", "CA1005: Avoid excessive parameters on generic types")]
public static class GenericDivision<TDividend, TDivisor, TQuotient>
{
    static readonly Lazy<Func<TDividend, TDivisor, TQuotient>> lazyFunc = new(() =>
    {
        var dividend = Expression.Parameter(typeof(TDividend), "dividend");
        var divisor = Expression.Parameter(typeof(TDivisor), "divisor");
        return Expression.Lambda<Func<TDividend, TDivisor, TQuotient>>(Expression.Divide(dividend, divisor), dividend, divisor).Compile();
    }, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Gets the instance
    /// </summary>
    public static Func<TDividend, TDivisor, TQuotient> Instance =>
        lazyFunc.Value;
}
