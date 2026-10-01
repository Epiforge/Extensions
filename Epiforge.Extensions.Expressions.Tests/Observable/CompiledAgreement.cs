namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Holds observations of every primitive operator and conversion an expression tree can express to what the same lambda gives when compiled, through direct subscription and through the graph, which without dynamic code are evaluated by closures rather than by the interpreter that compiles the lambda here
/// </summary>
[TestClass]
public class CompiledAgreement
{
    public enum ByteShade :
        byte
    {
        Light = 1,
        Dark = 200
    }

    public enum Shade
    {
        Light = 1,
        Dark = -7
    }

    public enum WideShade :
        long
    {
        Light = 1,
        Dark = long.MaxValue
    }

    public sealed class Operands :
        INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public object? Left { get; private set; }

        public object? Right { get; private set; }

        public void Set(object? left, object? right)
        {
            Left = left;
            Right = right;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Left)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Right)));
        }
    }

    static readonly Type[] numericTypes = [typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(char), typeof(float), typeof(double), typeof(decimal)];

    static readonly ExpressionType[] binaryOperators =
    [
        ExpressionType.Add, ExpressionType.AddChecked, ExpressionType.Subtract, ExpressionType.SubtractChecked, ExpressionType.Multiply, ExpressionType.MultiplyChecked, ExpressionType.Divide, ExpressionType.Modulo, ExpressionType.Power,
        ExpressionType.And, ExpressionType.Or, ExpressionType.ExclusiveOr, ExpressionType.LeftShift, ExpressionType.RightShift,
        ExpressionType.Equal, ExpressionType.NotEqual, ExpressionType.LessThan, ExpressionType.LessThanOrEqual, ExpressionType.GreaterThan, ExpressionType.GreaterThanOrEqual
    ];

    static readonly ExpressionType[] unaryOperators = [ExpressionType.Negate, ExpressionType.NegateChecked, ExpressionType.Not, ExpressionType.OnesComplement, ExpressionType.UnaryPlus, ExpressionType.IsTrue, ExpressionType.IsFalse];

    static object?[] SamplesOf(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return [null, .. SamplesOf(underlying)];
        if (type == typeof(bool))
            return [false, true];
        if (type == typeof(char))
            return ['\0', 'a', char.MaxValue];
        if (type == typeof(float))
            return [0f, 1f, -1f, 0.5f, -2.5f, float.MaxValue, float.MinValue, float.NaN, float.PositiveInfinity, 3e9f, -3e9f, 1e20f];
        if (type == typeof(double))
            return [0d, 1d, -1d, 0.5d, -2.5d, double.MaxValue, double.MinValue, double.NaN, double.NegativeInfinity, 3e9d, -3e9d, 1e20d];
        if (type == typeof(decimal))
            return [0m, 1m, -1m, 0.5m, decimal.MaxValue, decimal.MinValue];
        if (type == typeof(string))
            return [null, string.Empty, "a"];
        if (type.IsEnum)
            return [.. Enum.GetValues(type).Cast<object>(), Enum.ToObject(type, 0)];
        var minimum = type.GetField("MinValue")!.GetValue(null)!;
        var maximum = type.GetField("MaxValue")!.GetValue(null)!;
        var samples = new List<object> { Convert.ChangeType(0, type), Convert.ChangeType(1, type), Convert.ChangeType(2, type), Convert.ChangeType(7, type), minimum, maximum };
        if (!Equals(minimum, Convert.ChangeType(0, type)))
            samples.Add(Convert.ChangeType(-1, type));
        return [.. samples];
    }

    static string Describe(object? value) =>
        value is null ? "null" : $"{value} ({value.GetType().Name})";

    static (Type? Fault, object? Result) Outcome(Func<object?> evaluate)
    {
        try
        {
            return (null, evaluate());
        }
        catch (Exception ex)
        {
            return (ex.GetType(), null);
        }
    }

    static bool Agree((Type? Fault, object? Result) expected, (Exception? Fault, object? Result) observed) =>
        expected.Fault is { } fault ? observed.Fault?.GetType() == fault : observed.Fault is null && Equals(expected.Result, observed.Result) && expected.Result?.GetType() == observed.Result?.GetType();

    static string Describe((Type? Fault, object? Result) outcome) =>
        outcome.Fault is { } fault ? $"! {fault.Name}" : $"= {Describe(outcome.Result)}";

    static void Check(Expression<Func<Operands, object?>> lambda, IEnumerable<(object? Left, object? Right)> pairs, List<string> disagreements)
    {
        var reference = lambda.Compile();
        foreach (var useDirectSubscription in new[] { true, false })
        {
            var operands = new Operands();
            var pairList = pairs.ToList();
            operands.Set(pairList[0].Left, pairList[0].Right);
            var observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription });
            using var observation = observer.Observe(lambda, operands);
            foreach (var (left, right) in pairList)
            {
                operands.Set(left, right);
                var expected = Outcome(() => reference(operands));
                var observed = observation.Evaluation;
                if (!Agree(expected, observed) && disagreements.Count < 40)
                    disagreements.Add($"{(useDirectSubscription ? "direct" : "graph")} {lambda.Body} with {Describe(left)}, {Describe(right)}: compiled {Describe(expected)}, observed {Describe((observed.Fault?.GetType(), observed.Result))}");
            }
        }
    }

    static Expression OperandOf(ParameterExpression operands, string name, Type type) =>
        Expression.Convert(Expression.Property(operands, name), type);

    static void AssertAgreement(List<string> disagreements, int checkedCount)
    {
        Assert.IsTrue(checkedCount > 0);
        Assert.AreEqual(0, disagreements.Count, string.Join(Environment.NewLine, disagreements));
    }

    [TestMethod]
    public void BinaryOperatorsAgree()
    {
        var disagreements = new List<string>();
        var checkedCount = 0;
        var types = numericTypes.Concat([typeof(bool), typeof(string), typeof(Shade)]).ToList();
        types.AddRange(numericTypes.Concat([typeof(bool), typeof(Shade)]).Select(type => typeof(Nullable<>).MakeGenericType(type)).ToList());
        foreach (var type in types)
            foreach (var nodeType in binaryOperators)
            {
                var operands = Expression.Parameter(typeof(Operands));
                var shifts = nodeType is ExpressionType.LeftShift or ExpressionType.RightShift;
                var rightType = shifts ? Nullable.GetUnderlyingType(type) is null ? typeof(int) : typeof(int?) : type;
                Expression body;
                try
                {
                    body = Expression.MakeBinary(nodeType, OperandOf(operands, nameof(Operands.Left), type), OperandOf(operands, nameof(Operands.Right), rightType));
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                var lambda = Expression.Lambda<Func<Operands, object?>>(Expression.Convert(body, typeof(object)), operands);
                var rightSamples = shifts ? SamplesOf(rightType).Concat([31, 33, -1, 64]).Distinct().ToArray() : SamplesOf(rightType);
                Check(lambda, from left in SamplesOf(type) from right in rightSamples select (left, right), disagreements);
                ++checkedCount;
            }
        AssertAgreement(disagreements, checkedCount);
    }

    [TestMethod]
    public void ConversionsAgree()
    {
        var disagreements = new List<string>();
        var checkedCount = 0;
        var types = numericTypes.Concat([typeof(Shade), typeof(ByteShade), typeof(WideShade)]).ToList();
        types.AddRange(types.Select(type => typeof(Nullable<>).MakeGenericType(type)).ToList());
        types.Add(typeof(object));
        foreach (var from in types)
            foreach (var to in types)
                foreach (var nodeType in new[] { ExpressionType.Convert, ExpressionType.ConvertChecked })
                {
                    var operands = Expression.Parameter(typeof(Operands));
                    Expression body;
                    try
                    {
                        body = Expression.MakeUnary(nodeType, OperandOf(operands, nameof(Operands.Left), from), to);
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                    var lambda = Expression.Lambda<Func<Operands, object?>>(Expression.Convert(body, typeof(object)), operands);
                    var samples = from == typeof(object) ? types.Where(type => type != typeof(object)).SelectMany(SamplesOf).Distinct().ToArray() : SamplesOf(from);
                    Check(lambda, samples.Select(sample => (sample, (object?)null)), disagreements);
                    ++checkedCount;
                }
        AssertAgreement(disagreements, checkedCount);
    }

    [TestMethod]
    public void UnaryOperatorsAgree()
    {
        var disagreements = new List<string>();
        var checkedCount = 0;
        var types = numericTypes.Concat([typeof(bool), typeof(Shade)]).ToList();
        types.AddRange(types.Select(type => typeof(Nullable<>).MakeGenericType(type)).ToList());
        foreach (var type in types)
            foreach (var nodeType in unaryOperators)
            {
                var operands = Expression.Parameter(typeof(Operands));
                Expression body;
                try
                {
                    body = Expression.MakeUnary(nodeType, OperandOf(operands, nameof(Operands.Left), type), type);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                var lambda = Expression.Lambda<Func<Operands, object?>>(Expression.Convert(body, typeof(object)), operands);
                Check(lambda, SamplesOf(type).Select(sample => (sample, (object?)null)), disagreements);
                ++checkedCount;
            }
        AssertAgreement(disagreements, checkedCount);
    }
}
