namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class DifferentialFuzz
{
    sealed class World
    {
        internal World(int seed, bool useDirectSubscription)
        {
            var log = new SubscriptionLog();
            Log = log;
            Items = [];
            Other = new Recorded(log) { Rank = 3, Score = 5, Tag = "o" };
            Subject = new Recorded(log) { Rank = 7, Score = 2, Tag = "s" };
            Observer = new ExpressionObserver(Configured(seed, useDirectSubscription));
            (OtherMember, ItemsMember) = CaptureOf(Other, Items);
        }

        internal readonly ObservableRangeCollection<Recorded> Items;
        internal readonly MemberExpression ItemsMember;
        internal readonly SubscriptionLog Log;
        internal readonly ExpressionObserver Observer;
        internal readonly Recorded Other;
        internal readonly MemberExpression OtherMember;
        internal readonly Recorded Subject;

        internal int Notifications;

        internal Recorded Chosen(int which) =>
            which == 0 ? Subject : Other;
    }

    static readonly PropertyInfo count = typeof(ObservableRangeCollection<Recorded>).GetProperty(nameof(ObservableRangeCollection<Recorded>.Count))!;
    static readonly PropertyInfo next = typeof(Recorded).GetProperty(nameof(Recorded.Next))!;
    static readonly PropertyInfo rank = typeof(Recorded).GetProperty(nameof(Recorded.Rank))!;
    static readonly PropertyInfo score = typeof(Recorded).GetProperty(nameof(Recorded.Score))!;
    static readonly PropertyInfo tag = typeof(Recorded).GetProperty(nameof(Recorded.Tag))!;

    static ExpressionObserverOptions Configured(int seed, bool useDirectSubscription)
    {
        var rng = new Random(seed ^ 0x0b71);
        var options = new ExpressionObserverOptions
        {
            MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged = rng.Next(4) != 0,
            MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged = rng.Next(4) != 0,
            UseDirectSubscription = useDirectSubscription
        };
        if (rng.Next(5) == 0)
            options.AddIgnoredPropertyChangeNotification(rank);
        if (rng.Next(7) == 0)
            options.AddIgnoredPropertyChangeNotification(score);
        return options;
    }

    static (MemberExpression Other, MemberExpression Items) CaptureOf(Recorded other, ObservableRangeCollection<Recorded> items)
    {
        Expression<Func<bool>> capture = () => other.Rank > 0 & items.Count > 0;
        var body = (BinaryExpression)capture.Body;
        return ((MemberExpression)((MemberExpression)((BinaryExpression)body.Left).Left).Expression!, (MemberExpression)((MemberExpression)((BinaryExpression)body.Right).Left).Expression!);
    }

    static Expression Boolean(Random rng, int depth, ParameterExpression subject, MemberExpression other, MemberExpression items) =>
        depth <= 0 ? Expression.Constant(rng.Next(2) == 0) : (rng.Next(8) switch
        {
            0 => Expression.GreaterThan(Integer(rng, depth - 1, subject, other, items), Integer(rng, depth - 1, subject, other, items)),
            1 => Expression.LessThan(Integer(rng, depth - 1, subject, other, items), Integer(rng, depth - 1, subject, other, items)),
            2 => Expression.Equal(Integer(rng, depth - 1, subject, other, items), Integer(rng, depth - 1, subject, other, items)),
            3 => Expression.And(Boolean(rng, depth - 1, subject, other, items), Boolean(rng, depth - 1, subject, other, items)),
            4 => Expression.Or(Boolean(rng, depth - 1, subject, other, items), Boolean(rng, depth - 1, subject, other, items)),
            5 => Expression.AndAlso(Boolean(rng, depth - 1, subject, other, items), Boolean(rng, depth - 1, subject, other, items)),
            6 => Expression.Equal(Text(rng, depth - 1, subject, other, items), Text(rng, depth - 1, subject, other, items)),
            _ => Expression.TypeIs(Text(rng, depth - 1, subject, other, items), typeof(string))
        });

    static Expression Integer(Random rng, int depth, ParameterExpression subject, MemberExpression other, MemberExpression items) =>
        depth <= 0 ? Leaf(rng, subject, other, items) : (rng.Next(7) switch
        {
            0 => Expression.Add(Integer(rng, depth - 1, subject, other, items), Integer(rng, depth - 1, subject, other, items)),
            1 => Expression.Subtract(Integer(rng, depth - 1, subject, other, items), Integer(rng, depth - 1, subject, other, items)),
            2 => Expression.Multiply(Integer(rng, depth - 1, subject, other, items), Integer(rng, depth - 1, subject, other, items)),
            3 => Expression.Divide(Integer(rng, depth - 1, subject, other, items), Integer(rng, depth - 1, subject, other, items)),
            4 => Expression.Negate(Integer(rng, depth - 1, subject, other, items)),
            5 => Expression.Condition(Boolean(rng, depth - 1, subject, other, items), Integer(rng, depth - 1, subject, other, items), Integer(rng, depth - 1, subject, other, items)),
            _ => Leaf(rng, subject, other, items)
        });

    static Expression Leaf(Random rng, ParameterExpression subject, MemberExpression other, MemberExpression items) =>
        rng.Next(7) switch
        {
            0 => Expression.MakeMemberAccess(subject, rank),
            1 => Expression.MakeMemberAccess(subject, score),
            2 => Expression.MakeMemberAccess(other, rank),
            3 => Expression.MakeMemberAccess(other, score),
            4 => Expression.MakeMemberAccess(Expression.MakeMemberAccess(subject, next), rank),
            5 => Expression.MakeMemberAccess(items, count),
            _ => Expression.Constant(rng.Next(1, 5))
        };

    static Expression Text(Random rng, int depth, ParameterExpression subject, MemberExpression other, MemberExpression items) =>
        depth <= 0 ? TextLeaf(rng, subject, other, items) : (rng.Next(3) switch
        {
            0 => Expression.Coalesce(Text(rng, depth - 1, subject, other, items), Text(rng, depth - 1, subject, other, items)),
            1 => Expression.Condition(Boolean(rng, depth - 1, subject, other, items), Text(rng, depth - 1, subject, other, items), Text(rng, depth - 1, subject, other, items)),
            _ => TextLeaf(rng, subject, other, items)
        });

    static Expression TextLeaf(Random rng, ParameterExpression subject, MemberExpression other, MemberExpression items) =>
        rng.Next(4) switch
        {
            0 => Expression.MakeMemberAccess(subject, tag),
            1 => Expression.MakeMemberAccess(other, tag),
            2 => Expression.MakeMemberAccess(Expression.MakeMemberAccess(subject, next), tag),
            _ => Expression.Constant(rng.Next(2) == 0 ? "s" : null, typeof(string))
        };

    static Expression<Func<Recorded, object?>> Lambda(int seed, int depth, MemberExpression other, MemberExpression items)
    {
        var rng = new Random(seed);
        var subject = Expression.Parameter(typeof(Recorded), "s");
        var body = rng.Next(3) switch
        {
            0 => Boolean(rng, depth, subject, other, items),
            1 => Text(rng, depth, subject, other, items),
            _ => Integer(rng, depth, subject, other, items)
        };
        return Expression.Lambda<Func<Recorded, object?>>(Expression.Convert(body, typeof(object)), subject);
    }

    static string Describe((Exception? Fault, object? Result) evaluation) =>
        evaluation.Fault is { } fault ? $"!{fault.GetType().Name}" : evaluation.Result?.ToString() ?? "<null>";

    static void Mutate(World world, Random rng, int step)
    {
        var target = world.Chosen(rng.Next(2));
        switch (rng.Next(8))
        {
            case 5:
                world.Items.Add(new Recorded(world.Log) { Rank = rng.Next(0, 4) });
                break;
            case 6:
                if (world.Items.Count > 0)
                    world.Items.RemoveAt(world.Items.Count - 1);
                break;
            case 7:
                world.Items.Clear();
                break;
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
        var graphWorld = new World(seed, false);
        var fastWorld = new World(seed, true);
        var graphLambda = Lambda(seed, depth, graphWorld.OtherMember, graphWorld.ItemsMember);
        var fastLambda = Lambda(seed, depth, fastWorld.OtherMember, fastWorld.ItemsMember);
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
