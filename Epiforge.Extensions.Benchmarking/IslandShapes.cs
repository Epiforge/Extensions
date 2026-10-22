namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// An expression refused at its root, because it constructs a value the observer disposes of from an operand which can change, and that operand on its own, which the fast path admits; the operand sums as many of a wide object's properties as is asked
/// </summary>
static class IslandShapes
{
    static readonly ConstructorInfo leaseConstructor = typeof(BenchmarkLease).GetConstructor([typeof(int)])!;
    static readonly string[] properties = [nameof(BenchmarkWide.V0), nameof(BenchmarkWide.V1), nameof(BenchmarkWide.V2), nameof(BenchmarkWide.V3), nameof(BenchmarkWide.V4), nameof(BenchmarkWide.V5), nameof(BenchmarkWide.V6), nameof(BenchmarkWide.V7)];

    static Expression Operand(ParameterExpression wide, int width)
    {
        Expression operand = Expression.Property(wide, properties[0]);
        for (var i = 1; i < width; ++i)
            operand = Expression.Add(operand, Expression.Property(wide, properties[i]));
        return operand;
    }

    /// <summary>
    /// The operand alone, which the fast path serves and the graph builds a node for each read and each addition of
    /// </summary>
    internal static Expression<Func<BenchmarkWide, int>> Operand(int width)
    {
        var wide = Expression.Parameter(typeof(BenchmarkWide), "wide");
        return Expression.Lambda<Func<BenchmarkWide, int>>(Operand(wide, width), wide);
    }

    /// <summary>
    /// The operand passed to the construction of a lease, which refuses the whole expression
    /// </summary>
    internal static Expression<Func<BenchmarkWide, BenchmarkLease>> Refused(int width)
    {
        var wide = Expression.Parameter(typeof(BenchmarkWide), "wide");
        return Expression.Lambda<Func<BenchmarkWide, BenchmarkLease>>(Expression.New(leaseConstructor, Operand(wide, width)), wide);
    }
}
