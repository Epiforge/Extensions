namespace Epiforge.Extensions.Benchmarking;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
public class DirectSubscriptionOverheadBenchmarks
{
    DirectSubscriptionAnalyzer analyzer = null!;
    ConstantExpression closure = null!;
    ParameterExpression parameter = null!;
    PropertyInfo rank = null!;
    BenchmarkPerson subject = null!;
    FieldInfo thresholdField = null!;

    MemberExpression BuildClosureThresholdRank() =>
        Expression.MakeMemberAccess(Expression.MakeMemberAccess(closure, thresholdField), rank);

    BinaryExpression BuildNormalizedComparison() =>
        Expression.GreaterThan(BuildNormalizedSelector(), BuildClosureThresholdRank());

    MemberExpression BuildNormalizedSelector() =>
        Expression.MakeMemberAccess(Expression.Constant(subject, typeof(BenchmarkPerson)), rank);

    [Benchmark]
    public Expression BuildComparisonOnly() =>
        BuildNormalizedComparison();

    [Benchmark(Baseline = true)]
    public Expression BuildSelectorOnly() =>
        BuildNormalizedSelector();

    [Benchmark]
    public DirectSubscriptionAnalysis BuildThenAnalyzeSelector() =>
        analyzer.Analyze(BuildNormalizedSelector());

    [Benchmark]
    public int BuildThenHashComparisonLambda() =>
        ExpressionEqualityComparer.Default.GetHashCode(Expression.Lambda<Func<BenchmarkPerson, bool>>(Expression.GreaterThan(Expression.MakeMemberAccess(parameter, rank), BuildClosureThresholdRank()), parameter));

    [Benchmark]
    public int BuildThenHashSelectorLambda() =>
        ExpressionEqualityComparer.Default.GetHashCode(Expression.Lambda<Func<BenchmarkPerson, int>>(Expression.MakeMemberAccess(parameter, rank), parameter));

    [Benchmark]
    public DirectSubscriptionPlan BuildThenPlanComparison() =>
        analyzer.Plan(BuildNormalizedComparison());

    [Benchmark]
    public DirectSubscriptionPlan BuildThenPlanSelector() =>
        analyzer.Plan(BuildNormalizedSelector());

    [GlobalSetup]
    public void Setup()
    {
        var threshold = new BenchmarkPerson("threshold", 4);
        Expression<Func<bool>> capturing = () => threshold.Rank > 0;
        var thresholdRank = (MemberExpression)((BinaryExpression)capturing.Body).Left;
        var thresholdMember = (MemberExpression)thresholdRank.Expression!;
        analyzer = new DirectSubscriptionAnalyzer();
        closure = (ConstantExpression)thresholdMember.Expression!;
        parameter = Expression.Parameter(typeof(BenchmarkPerson));
        rank = typeof(BenchmarkPerson).GetProperty(nameof(BenchmarkPerson.Rank))!;
        subject = new BenchmarkPerson("subject", 4);
        thresholdField = (FieldInfo)thresholdMember.Member;
    }
}
