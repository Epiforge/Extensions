namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class DifferentialFuzz
{
    sealed class World
    {
        internal World(bool useDirectSubscription)
        {
            var log = new SubscriptionLog();
            Log = log;
            Other = new Recorded(log) { Rank = 3, Score = 5, Tag = "o" };
            Subject = new Recorded(log) { Rank = 7, Score = 2, Tag = "s" };
            Observer = new ExpressionObserver(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription });
            OtherMember = CaptureOf(Other);
        }

        internal readonly SubscriptionLog Log;
        internal readonly ExpressionObserver Observer;
        internal readonly Recorded Other;
        internal readonly MemberExpression OtherMember;
        internal readonly Recorded Subject;

        internal int Notifications;

        internal Recorded Chosen(int which) =>
            which == 0 ? Subject : Other;
    }

    static readonly PropertyInfo next = typeof(Recorded).GetProperty(nameof(Recorded.Next))!;
    static readonly PropertyInfo rank = typeof(Recorded).GetProperty(nameof(Recorded.Rank))!;
    static readonly PropertyInfo score = typeof(Recorded).GetProperty(nameof(Recorded.Score))!;
    static readonly PropertyInfo tag = typeof(Recorded).GetProperty(nameof(Recorded.Tag))!;

    static MemberExpression CaptureOf(Recorded other)
    {
        Expression<Func<bool>> capture = () => other.Rank > 0;
        return (MemberExpression)((MemberExpression)((BinaryExpression)capture.Body).Left).Expression!;
    }

    static Expression Boolean(Random rng, int depth, ParameterExpression subject, MemberExpression other) =>
        depth <= 0 ? Expression.Constant(rng.Next(2) == 0) : (rng.Next(8) switch
        {
            0 => Expression.GreaterThan(Integer(rng, depth - 1, subject, other), Integer(rng, depth - 1, subject, other)),
            1 => Expression.LessThan(Integer(rng, depth - 1, subject, other), Integer(rng, depth - 1, subject, other)),
            2 => Expression.Equal(Integer(rng, depth - 1, subject, other), Integer(rng, depth - 1, subject, other)),
            3 => Expression.And(Boolean(rng, depth - 1, subject, other), Boolean(rng, depth - 1, subject, other)),
            4 => Expression.Or(Boolean(rng, depth - 1, subject, other), Boolean(rng, depth - 1, subject, other)),
            5 => Expression.AndAlso(Boolean(rng, depth - 1, subject, other), Boolean(rng, depth - 1, subject, other)),
            6 => Expression.Equal(Text(rng, depth - 1, subject, other), Text(rng, depth - 1, subject, other)),
            _ => Expression.TypeIs(Text(rng, depth - 1, subject, other), typeof(string))
        });

    static Expression Integer(Random rng, int depth, ParameterExpression subject, MemberExpression other) =>
        depth <= 0 ? Leaf(rng, subject, other) : (rng.Next(7) switch
        {
            0 => Expression.Add(Integer(rng, depth - 1, subject, other), Integer(rng, depth - 1, subject, other)),
            1 => Expression.Subtract(Integer(rng, depth - 1, subject, other), Integer(rng, depth - 1, subject, other)),
            2 => Expression.Multiply(Integer(rng, depth - 1, subject, other), Integer(rng, depth - 1, subject, other)),
            3 => Expression.Divide(Integer(rng, depth - 1, subject, other), Integer(rng, depth - 1, subject, other)),
            4 => Expression.Negate(Integer(rng, depth - 1, subject, other)),
            5 => Expression.Condition(Boolean(rng, depth - 1, subject, other), Integer(rng, depth - 1, subject, other), Integer(rng, depth - 1, subject, other)),
            _ => Leaf(rng, subject, other)
        });

    static Expression Leaf(Random rng, ParameterExpression subject, MemberExpression other) =>
        rng.Next(6) switch
        {
            0 => Expression.MakeMemberAccess(subject, rank),
            1 => Expression.MakeMemberAccess(subject, score),
            2 => Expression.MakeMemberAccess(other, rank),
            3 => Expression.MakeMemberAccess(other, score),
            4 => Expression.MakeMemberAccess(Expression.MakeMemberAccess(subject, next), rank),
            _ => Expression.Constant(rng.Next(1, 5))
        };

    static Expression Text(Random rng, int depth, ParameterExpression subject, MemberExpression other) =>
        depth <= 0 ? TextLeaf(rng, subject, other) : (rng.Next(3) switch
        {
            0 => Expression.Coalesce(Text(rng, depth - 1, subject, other), Text(rng, depth - 1, subject, other)),
            1 => Expression.Condition(Boolean(rng, depth - 1, subject, other), Text(rng, depth - 1, subject, other), Text(rng, depth - 1, subject, other)),
            _ => TextLeaf(rng, subject, other)
        });

    static Expression TextLeaf(Random rng, ParameterExpression subject, MemberExpression other) =>
        rng.Next(4) switch
        {
            0 => Expression.MakeMemberAccess(subject, tag),
            1 => Expression.MakeMemberAccess(other, tag),
            2 => Expression.MakeMemberAccess(Expression.MakeMemberAccess(subject, next), tag),
            _ => Expression.Constant(rng.Next(2) == 0 ? "s" : null, typeof(string))
        };

    static Expression<Func<Recorded, object?>> Lambda(int seed, int depth, MemberExpression other)
    {
        var rng = new Random(seed);
        var subject = Expression.Parameter(typeof(Recorded), "s");
        var body = rng.Next(3) switch
        {
            0 => Boolean(rng, depth, subject, other),
            1 => Text(rng, depth, subject, other),
            _ => Integer(rng, depth, subject, other)
        };
        return Expression.Lambda<Func<Recorded, object?>>(Expression.Convert(body, typeof(object)), subject);
    }

    static string Describe((Exception? Fault, object? Result) evaluation) =>
        evaluation.Fault is { } fault ? $"!{fault.GetType().Name}" : evaluation.Result?.ToString() ?? "<null>";

    static void Mutate(World world, Random rng, int step)
    {
        var target = world.Chosen(rng.Next(2));
        switch (rng.Next(5))
        {
            case 0:
                target.Rank = rng.Next(0, 4);
                break;
            case 1:
                target.Score = rng.Next(0, 4);
                break;
            case 2:
                target.Tag = rng.Next(3) switch { 0 => null, 1 => "s", _ => $"t{step}" };
                break;
            case 3:
                target.Next = rng.Next(2) == 0 ? null : new Recorded(world.Log) { Rank = rng.Next(0, 4), Score = rng.Next(0, 4), Tag = "n" };
                break;
            default:
                target.Rank ^= 1;
                break;
        }
    }

    static void RunProgram(int seed, int depth, int steps)
    {
        var graphWorld = new World(false);
        var fastWorld = new World(true);
        var graphLambda = Lambda(seed, depth, graphWorld.OtherMember);
        var fastLambda = Lambda(seed, depth, fastWorld.OtherMember);
        Assert.AreEqual(graphLambda.ToString().Replace("value(", "@("), fastLambda.ToString().Replace("value(", "@("), $"seed {seed}: the two worlds were given different expressions");
        using var graphExpression = graphWorld.Observer.Observe(graphLambda, graphWorld.Subject);
        using var fastExpression = fastWorld.Observer.Observe(fastLambda, fastWorld.Subject);
        graphExpression.PropertyChanged += (sender, e) => ++graphWorld.Notifications;
        fastExpression.PropertyChanged += (sender, e) => ++fastWorld.Notifications;
        Assert.AreEqual(Describe(graphExpression.Evaluation), Describe(fastExpression.Evaluation), $"seed {seed}: initial evaluation diverged for {graphLambda}");
        var graphRng = new Random(seed ^ 0x5eed);
        var fastRng = new Random(seed ^ 0x5eed);
        for (var step = 0; step < steps; ++step)
        {
            Mutate(graphWorld, graphRng, step);
            Mutate(fastWorld, fastRng, step);
            Assert.AreEqual(Describe(graphExpression.Evaluation), Describe(fastExpression.Evaluation), $"seed {seed}, step {step}: evaluation diverged for {graphLambda}");
            Assert.AreEqual(graphWorld.Notifications, fastWorld.Notifications, $"seed {seed}, step {step}: notification count diverged for {graphLambda}");
        }
    }

    [TestMethod]
    public void DeepExpressions()
    {
        for (var seed = 5000; seed < 5150; ++seed)
            RunProgram(seed, 4, 12);
    }

    [TestMethod]
    public void ShallowExpressions()
    {
        for (var seed = 1000; seed < 1300; ++seed)
            RunProgram(seed, 2, 16);
    }

    [TestMethod]
    public void SingleMemberExpressions()
    {
        for (var seed = 9000; seed < 9200; ++seed)
            RunProgram(seed, 0, 20);
    }
}
