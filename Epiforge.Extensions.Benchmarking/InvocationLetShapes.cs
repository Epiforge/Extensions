namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// The duration format an application's formula engine emits as an invocation of a literal lambda whose parameter is read eight times, some of them in conditional branches, and the same body with the invocation written out by hand
/// </summary>
static class InvocationLetShapes
{
    const string pattern = "{4}{5}{0}{3}{1:00}{3}{2:00}{6}";

    static readonly IFormatProvider formatProvider = CultureInfo.InvariantCulture;
    static readonly NumberFormatInfo numberFormatPattern = NumberFormatInfo.InvariantInfo;
    static readonly PropertyInfo elapsedProperty = typeof(BenchmarkDuration).GetProperty(nameof(BenchmarkDuration.Elapsed))!;

    static readonly Expression<Func<TimeSpan, string>> format = ts => string.Format(formatProvider, pattern, new object?[] { Math.Abs(ts.Days) * 24 + Math.Abs(ts.Hours), Math.Abs(ts.Minutes), Math.Abs(ts.TotalSeconds) % 60D, DateTimeFormatInfo.GetInstance(formatProvider).TimeSeparator, ts < TimeSpan.Zero ? numberFormatPattern.NegativeSign : string.Empty, ts < TimeSpan.Zero ? "(" : string.Empty, ts < TimeSpan.Zero ? ")" : string.Empty });

    /// <summary>
    /// The format's body with every read of its parameter replaced by a read of the duration, which the fast path serves without the invocation, at the price of reading the property once for each place the parameter was read
    /// </summary>
    internal static readonly Expression<Func<BenchmarkDuration, string>> HandReduced = Build(false);

    /// <summary>
    /// The format invoked on the duration's elapsed time, as the formula engine emits it
    /// </summary>
    internal static readonly Expression<Func<BenchmarkDuration, string>> Let = Build(true);

    static Expression<Func<BenchmarkDuration, string>> Build(bool invoked)
    {
        var duration = Expression.Parameter(typeof(BenchmarkDuration), "duration");
        var elapsed = Expression.MakeMemberAccess(duration, elapsedProperty);
        var body = invoked ? (Expression)Expression.Invoke(format, elapsed) : new Substitution(format.Parameters[0], elapsed).Visit(format.Body)!;
        return Expression.Lambda<Func<BenchmarkDuration, string>>(body, duration);
    }

    sealed class Substitution(ParameterExpression parameter, Expression replacement) :
        ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }
}
