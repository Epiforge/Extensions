namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class DifferentialFuzz
{
    public sealed class FieldHolder
    {
        public Recorded? Held;
        public ObservableRangeCollection<Recorded> HeldItems = [];
    }

    sealed class Sources(ParameterExpression subject, MemberExpression other, MemberExpression items, MemberExpression held, MemberExpression heldItems)
    {
        internal readonly MemberExpression Held = held;
        internal readonly MemberExpression HeldItems = heldItems;
        internal readonly MemberExpression Items = items;
        internal readonly MemberExpression Other = other;
        internal readonly ParameterExpression Subject = subject;
    }

    sealed class World
    {
        internal World(int seed, bool useDirectSubscription)
        {
            var log = new SubscriptionLog();
            Log = log;
            Items = [];
            Other = new Recorded(log) { Rank = 3, Score = 5, Tag = "o", Linked = new Recorded(log) { Rank = 4, Score = 1, Tag = "ol" } };
            Subject = new Recorded(log) { Rank = 7, Score = 2, Tag = "s", Linked = new Recorded(log) { Rank = 2, Score = 6, Tag = "l" } };
            Holder = new FieldHolder { Held = new Recorded(log) { Rank = 1, Score = 4, Tag = "h" } };
            Observer = new ExpressionObserver(Configured(seed, useDirectSubscription));
            (OtherMember, ItemsMember) = CaptureOf(Other, Items);
            var holder = Expression.Constant(Holder, typeof(FieldHolder));
            HeldMember = Expression.Field(holder, held);
            HeldItemsMember = Expression.Field(holder, heldItems);
        }

        internal readonly MemberExpression HeldItemsMember;
        internal readonly MemberExpression HeldMember;
        internal readonly FieldHolder Holder;
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
    static readonly FieldInfo held = typeof(FieldHolder).GetField(nameof(FieldHolder.Held))!;
    static readonly FieldInfo heldItems = typeof(FieldHolder).GetField(nameof(FieldHolder.HeldItems))!;
    static readonly FieldInfo linked = typeof(Recorded).GetField(nameof(Recorded.Linked))!;
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
        if (rng.Next(2) == 0)
            options.Optimizer = ExpressionOptimizer.tryVisit;
        return options;
    }

    static (MemberExpression Other, MemberExpression Items) CaptureOf(Recorded other, ObservableRangeCollection<Recorded> items)
    {
        Expression<Func<bool>> capture = () => other.Rank > 0 & items.Count > 0;
        var body = (BinaryExpression)capture.Body;
        return ((MemberExpression)((MemberExpression)((BinaryExpression)body.Left).Left).Expression!, (MemberExpression)((MemberExpression)((BinaryExpression)body.Right).Left).Expression!);
    }

    static Expression Boolean(Random rng, int depth, Sources sources) =>
        depth <= 0 ? Expression.Constant(rng.Next(2) == 0) : (rng.Next(8) switch
        {
            0 => Expression.GreaterThan(Integer(rng, depth - 1, sources), Integer(rng, depth - 1, sources)),
            1 => Expression.LessThan(Integer(rng, depth - 1, sources), Integer(rng, depth - 1, sources)),
            2 => Expression.Equal(Integer(rng, depth - 1, sources), Integer(rng, depth - 1, sources)),
            3 => Expression.And(Boolean(rng, depth - 1, sources), Boolean(rng, depth - 1, sources)),
            4 => Expression.Or(Boolean(rng, depth - 1, sources), Boolean(rng, depth - 1, sources)),
            5 => Expression.AndAlso(Boolean(rng, depth - 1, sources), Boolean(rng, depth - 1, sources)),
            6 => Expression.Equal(Text(rng, depth - 1, sources), Text(rng, depth - 1, sources)),
            _ => Expression.TypeIs(Text(rng, depth - 1, sources), typeof(string))
        });

    static Expression Integer(Random rng, int depth, Sources sources) =>
        depth <= 0 ? Leaf(rng, sources) : (rng.Next(7) switch
        {
            0 => Expression.Add(Integer(rng, depth - 1, sources), Integer(rng, depth - 1, sources)),
            1 => Expression.Subtract(Integer(rng, depth - 1, sources), Integer(rng, depth - 1, sources)),
            2 => Expression.Multiply(Integer(rng, depth - 1, sources), Integer(rng, depth - 1, sources)),
            3 => Expression.Divide(Integer(rng, depth - 1, sources), Integer(rng, depth - 1, sources)),
            4 => Expression.Negate(Integer(rng, depth - 1, sources)),
            5 => Expression.Condition(Boolean(rng, depth - 1, sources), Integer(rng, depth - 1, sources), Integer(rng, depth - 1, sources)),
            _ => Leaf(rng, sources)
        });

    static Expression Leaf(Random rng, Sources sources) =>
        rng.Next(11) switch
        {
            9 => Expression.MakeMemberAccess(Expression.Field(sources.Other, linked), rank),
            0 => Expression.MakeMemberAccess(sources.Subject, rank),
            1 => Expression.MakeMemberAccess(sources.Subject, score),
            2 => Expression.MakeMemberAccess(sources.Other, rank),
            3 => Expression.MakeMemberAccess(sources.Other, score),
            4 => Expression.MakeMemberAccess(Expression.MakeMemberAccess(sources.Subject, next), rank),
            5 => Expression.MakeMemberAccess(sources.Items, count),
            6 => Expression.MakeMemberAccess(sources.Held, rank),
            7 => Expression.MakeMemberAccess(sources.HeldItems, count),
            8 => Expression.MakeMemberAccess(Expression.Field(sources.Subject, linked), rank),
            _ => Expression.Constant(rng.Next(1, 5))
        };

    static Expression Text(Random rng, int depth, Sources sources) =>
        depth <= 0 ? TextLeaf(rng, sources) : (rng.Next(3) switch
        {
            0 => Expression.Coalesce(Text(rng, depth - 1, sources), Text(rng, depth - 1, sources)),
            1 => Expression.Condition(Boolean(rng, depth - 1, sources), Text(rng, depth - 1, sources), Text(rng, depth - 1, sources)),
            _ => TextLeaf(rng, sources)
        });

    static Expression TextLeaf(Random rng, Sources sources) =>
        rng.Next(5) switch
        {
            0 => Expression.MakeMemberAccess(sources.Subject, tag),
            1 => Expression.MakeMemberAccess(sources.Other, tag),
            2 => Expression.MakeMemberAccess(Expression.MakeMemberAccess(sources.Subject, next), tag),
            3 => Expression.MakeMemberAccess(sources.Held, tag),
            _ => Expression.Constant(rng.Next(2) == 0 ? "s" : null, typeof(string))
        };

    static Expression<Func<Recorded, object?>> Lambda(int seed, int depth, World world)
    {
        var rng = new Random(seed);
        var subject = Expression.Parameter(typeof(Recorded), "s");
        var sources = new Sources(subject, world.OtherMember, world.ItemsMember, world.HeldMember, world.HeldItemsMember);
        var body = rng.Next(3) switch
        {
            0 => Boolean(rng, depth, sources),
            1 => Text(rng, depth, sources),
            _ => Integer(rng, depth, sources)
        };
        return Expression.Lambda<Func<Recorded, object?>>(Expression.Convert(body, typeof(object)), subject);
    }

    static string Describe((Exception? Fault, object? Result) evaluation) =>
        evaluation.Fault is { } fault ? $"!{fault.GetType().Name}" : evaluation.Result?.ToString() ?? "<null>";

    static void Mutate(World world, Random rng, int step)
    {
        var target = world.Chosen(rng.Next(2));
        switch (rng.Next(13))
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
            case 8:
                world.Holder.Held!.Rank = rng.Next(0, 4);
                break;
            case 9:
                if (rng.Next(2) == 0)
                    world.Holder.HeldItems.Add(new Recorded(world.Log) { Rank = rng.Next(0, 4) });
                else if (world.Holder.HeldItems.Count > 0)
                    world.Holder.HeldItems.RemoveAt(world.Holder.HeldItems.Count - 1);
                break;
            case 10:
                world.Holder.Held = new Recorded(world.Log) { Rank = rng.Next(0, 4), Score = rng.Next(0, 4), Tag = "r" };
                break;
            case 11:
                world.Subject.Linked!.Rank = rng.Next(0, 4);
                break;
            case 12:
                world.Subject.Linked = new Recorded(world.Log) { Rank = rng.Next(0, 4), Score = rng.Next(0, 4), Tag = "k" };
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
        var graphLambda = Lambda(seed, depth, graphWorld);
        var fastLambda = Lambda(seed, depth, fastWorld);
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
}
