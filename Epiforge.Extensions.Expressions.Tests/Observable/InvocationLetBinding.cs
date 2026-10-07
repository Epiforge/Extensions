namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Invocations of literal lambdas which cannot be reduced to their bodies, because a parameter is read other than exactly once or is read where the body might not evaluate it, observed by the fast path as a let rather than refused
/// </summary>
/// <remarks>
/// Each test runs one script against both mechanisms and requires the same transcript from each: after every step, the result or the fault, the number of times the observation announced a new evaluation, and every attachment made to a recorded object. The fast path must also have served the observation itself, which an observer with nothing in its cache shows; without that, a fast path which declined would build the graph and agree with it vacuously
/// </remarks>
[TestClass]
public class InvocationLetBinding
{
    public sealed class Holder :
        INotifyPropertyChanged
    {
        ObservableCollection<int> items = [];

        public ObservableCollection<int> Items
        {
            get => items;
            set
            {
                items = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public sealed record TextKey(string Text);

    sealed class World
    {
        public readonly Holder Holder = new();
        public readonly SubscriptionLog Log = new();
        public readonly Recorded Subject;

        public World() =>
            Subject = new Recorded(Log) { Rank = 1, Score = 2, Tag = "tag", Next = new Recorded(Log) { Rank = 10, Score = 20 } };
    }

    static readonly ConstructorInfo textKeyConstructor = typeof(TextKey).GetConstructor([typeof(string)])!;

    static List<string> Run<TResult>(Func<World, Expression<Func<Recorded, TResult>>> build, Action<World, Action<string>> script, bool useDirectSubscription, bool expectDirect)
    {
        var world = new World();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var transcript = new List<string>();
        using (var expression = observer.Observe(build(world), world.Subject))
        {
            var notifications = 0;
            expression.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(IObservableExpression<object?>.Evaluation))
                    ++notifications;
            };
            void Step(string label)
            {
                if (useDirectSubscription)
                {
                    if (expectDirect)
                        Assert.AreEqual(0, observer.CachedObservableExpressions, $"{label}: the fast path did not serve the observation");
                    else
                        Assert.AreNotEqual(0, observer.CachedObservableExpressions, $"{label}: the fast path served an observation it should have refused");
                }
                var evaluation = expression.Evaluation;
                var outcome = evaluation.Fault is { } fault ? $"fault {fault.GetType().Name}: {fault.Message}" : $"result {evaluation.Result}";
                transcript.Add($"{label}: {outcome}; notifications {notifications}; attachments [{string.Join(", ", world.Log.Attachments())}]; outstanding {world.Log.Outstanding}");
            }
            Step("observed");
            script(world, Step);
        }
        transcript.Add($"disposed: outstanding {world.Log.Outstanding}");
        Assert.AreEqual(0, observer.CachedObservableExpressions, "the observer kept nodes after the observation was disposed");
        return transcript;
    }

    static void AssertAgreement<TResult>(Func<World, Expression<Func<Recorded, TResult>>> build, Action<World, Action<string>> script, bool expectDirect = true)
    {
        var graph = Run(build, script, false, expectDirect);
        var fast = Run(build, script, true, expectDirect);
        CollectionAssert.AreEqual(graph, fast, $"graph:{Environment.NewLine}{string.Join(Environment.NewLine, graph)}{Environment.NewLine}fast path:{Environment.NewLine}{string.Join(Environment.NewLine, fast)}");
    }

    static Expression<Func<Recorded, TResult>> Invoke<TResult>(LambdaExpression lambda, Func<ParameterExpression, Expression[]> arguments)
    {
        var s = Expression.Parameter(typeof(Recorded), "s");
        return Expression.Lambda<Func<Recorded, TResult>>(Expression.Invoke(lambda, arguments(s)), s);
    }

    static MemberExpression Property(Expression instance, string name) =>
        Expression.Property(instance, name);

    [TestMethod]
    public void AChangingArgumentIsFollowedWhereTheBodyReadsThroughIt()
    {
        Expression<Func<Recorded, int>> sum = n => n.Rank + n.Score;
        AssertAgreement(world => Invoke<int>(sum, s => [Property(s, nameof(Recorded.Next))]), (world, step) =>
        {
            var first = world.Subject.Next!;
            first.Rank = 11;
            step("first changed");
            world.Subject.Next = new Recorded(world.Log) { Rank = 100, Score = 200 };
            step("argument replaced");
            first.Score = 21;
            step("replaced argument changed");
            world.Subject.Next!.Score = 201;
            step("new argument changed");
        });
    }

    [TestMethod]
    public void AFaultingArgumentFaultsTheInvocation()
    {
        Expression<Func<int, int>> twice = x => x + x;
        AssertAgreement(world => Invoke<int>(twice, s => [Expression.Divide(Expression.Constant(10), Property(s, nameof(Recorded.Score)))]), (world, step) =>
        {
            world.Subject.Score = 0;
            step("faulted");
            world.Subject.Rank = 5;
            step("unread property changed while faulted");
            world.Subject.Score = 5;
            step("recovered");
        });
    }

    [TestMethod]
    public void AFaultingArgumentReleasesWhatTheBodyFollowed()
    {
        Expression<Func<Recorded, int>> sum = n => n.Rank + n.Score;
        AssertAgreement(world => Invoke<int>(sum, s => [Expression.ArrayIndex(Expression.NewArrayInit(typeof(Recorded), Property(s, nameof(Recorded.Next))), Expression.Subtract(Property(s, nameof(Recorded.Score)), Expression.Constant(2)))]), (world, step) =>
        {
            var first = world.Subject.Next!;
            first.Rank = 11;
            step("followed argument changed");
            world.Subject.Score = 3;
            step("argument faulted");
            first.Rank = 12;
            step("formerly followed argument changed while faulted");
            world.Subject.Score = 2;
            step("recovered");
            first.Score = 21;
            step("followed again");
        });
    }

    [TestMethod]
    public void AnArgumentReadOnlyInsideDeferredBranches()
    {
        Expression<Func<bool, int, int>> pick = (c, x) => c ? x * x : x == 0 ? 1 : 0;
        AssertAgreement(world => Invoke<int>(pick, s => [Expression.GreaterThan(Property(s, nameof(Recorded.Rank)), Expression.Constant(0)), Property(s, nameof(Recorded.Score))]), (world, step) =>
        {
            world.Subject.Score = 3;
            step("argument changed with the branch reading it taken");
            world.Subject.Rank = 0;
            step("other branch taken");
            world.Subject.Score = 0;
            step("argument changed with the other branch taken");
            world.Subject.Score = 4;
            step("argument changed again");
        });
    }

    [TestMethod]
    public void AnArgumentReadOnlyInsideDeferredBranchesStillFaults()
    {
        Expression<Func<bool, int, int>> pick = (c, x) => c ? x + x : 0;
        AssertAgreement(world => Invoke<int>(pick, s => [Expression.GreaterThan(Property(s, nameof(Recorded.Rank)), Expression.Constant(0)), Expression.Divide(Expression.Constant(10), Property(s, nameof(Recorded.Score)))]), (world, step) =>
        {
            world.Subject.Rank = 0;
            step("branch reading the argument not taken");
            world.Subject.Score = 0;
            step("argument faulted with its branch not taken");
            world.Subject.Rank = 1;
            step("branch taken while faulted");
            world.Subject.Score = 5;
            step("recovered");
        });
    }

    [TestMethod]
    public void AnArgumentTheBodyNeverReads()
    {
        Expression<Func<int, int>> constant = x => 5;
        AssertAgreement(world => Invoke<int>(constant, s => [Property(s, nameof(Recorded.Rank))]), (world, step) =>
        {
            world.Subject.Rank = 2;
            step("unread argument changed");
            world.Subject.Score = 3;
            step("unrelated property changed");
        });
    }

    [TestMethod]
    public void ACollectionValuedArgument()
    {
        Expression<Func<ObservableCollection<int>, int>> doubled = items => items.Count + items.Count;
        AssertAgreement(world => Invoke<int>(doubled, s => [Property(Expression.Constant(world.Holder), nameof(Holder.Items))]), (world, step) =>
        {
            var first = world.Holder.Items;
            first.Add(1);
            step("added");
            world.Holder.Items = [1, 2, 3];
            step("replaced");
            first.Add(2);
            step("replaced collection changed");
            world.Holder.Items.Add(4);
            step("new collection changed");
        });
    }

    [TestMethod]
    public void AMultiplyReadParameter()
    {
        Expression<Func<int, int>> poly = v => v * v + v * 2;
        AssertAgreement(world => Invoke<int>(poly, s => [Property(s, nameof(Recorded.Rank))]), (world, step) =>
        {
            world.Subject.Rank = 3;
            step("argument changed");
            world.Subject.Score = 7;
            step("unrelated property changed");
            world.Subject.Rank = 3;
            step("argument set to the same value");
        });
    }

    [TestMethod]
    public void ANestedLet()
    {
        Expression<Func<int, int>> square = b => b * b;
        var a = Expression.Parameter(typeof(int), "a");
        var outer = Expression.Lambda<Func<int, int>>(Expression.Add(Expression.Invoke(square, Expression.Add(a, a)), a), a);
        AssertAgreement(world => Invoke<int>(outer, s => [Property(s, nameof(Recorded.Rank))]), (world, step) =>
        {
            world.Subject.Rank = 2;
            step("argument changed");
            world.Subject.Rank = -2;
            step("argument changed again");
        });
    }

    [TestMethod]
    public void ATextSortKey()
    {
        Expression<Func<string?, TextKey?>> key = value => value == null ? null : new TextKey(value);
        AssertAgreement(world => Invoke<TextKey?>(key, s => [Property(s, nameof(Recorded.Tag))]), (world, step) =>
        {
            world.Subject.Tag = null;
            step("null");
            world.Subject.Tag = "second";
            step("not null again");
            world.Subject.Tag = "third";
            step("changed");
        });
    }

    [TestMethod]
    public void LetsWithinAnOrdinaryExpression()
    {
        Expression<Func<int, int>> twice = x => x + x;
        var s = Expression.Parameter(typeof(Recorded), "s");
        var lambda = Expression.Lambda<Func<Recorded, int>>(Expression.Multiply(Expression.Invoke(twice, Property(s, nameof(Recorded.Rank))), Expression.Invoke(twice, Property(s, nameof(Recorded.Score)))), s);
        AssertAgreement(world => lambda, (world, step) =>
        {
            world.Subject.Rank = 3;
            step("first argument changed");
            world.Subject.Score = 5;
            step("second argument changed");
        });
    }

    [TestMethod]
    public void TheFirstOfTwoFaultingArguments()
    {
        Expression<Func<int, int, int>> sum = (a, b) => a + a + b + b;
        AssertAgreement(world => Invoke<int>(sum, s => [Expression.Divide(Expression.Constant(10), Property(s, nameof(Recorded.Score))), Expression.AddChecked(Expression.Constant(int.MaxValue - 10), Property(s, nameof(Recorded.Rank)))]), (world, step) =>
        {
            world.Subject.Score = 0;
            step("first argument faulted");
            world.Subject.Rank = 20;
            step("both arguments faulted");
            world.Subject.Score = 5;
            step("only the second argument faulted");
            world.Subject.Rank = 2;
            step("recovered");
            world.Subject.Rank = 3;
            step("second argument changed");
        });
    }

    [TestMethod]
    public void TheGraphIsStillBuiltWhereTheBodyFollowsOneOfTwoArguments()
    {
        Expression<Func<int, Recorded, int>> sum = (a, b) => a + a + b.Rank;
        AssertAgreement(world => Invoke<int>(sum, s => [Expression.Divide(Expression.Constant(10), Property(s, nameof(Recorded.Score))), Property(s, nameof(Recorded.Next))]), (world, step) =>
        {
            world.Subject.Score = 5;
            step("argument the body does not follow changed");
            world.Subject.Next!.Rank = 11;
            step("followed argument changed");
            world.Subject.Score = 0;
            step("first argument faulted");
            world.Subject.Next = new Recorded(world.Log) { Rank = 30 };
            step("second argument replaced while the first faulted");
            world.Subject.Score = 2;
            step("recovered");
        }, false);
    }

    [TestMethod]
    public void TheGraphIsStillBuiltWhereABranchOfTheBodySubscribes()
    {
        Expression<Func<bool, Recorded, int>> pick = (c, n) => c ? n.Rank + n.Rank : 0;
        AssertAgreement(world => Invoke<int>(pick, s => [Expression.GreaterThan(Property(s, nameof(Recorded.Rank)), Expression.Constant(0)), Property(s, nameof(Recorded.Next))]), (world, step) =>
        {
            var first = world.Subject.Next!;
            world.Subject.Rank = 0;
            step("branch reading through the argument no longer taken");
            world.Subject.Next = new Recorded(world.Log) { Rank = 7 };
            step("argument replaced while the branch is not taken");
            first.Rank = 99;
            step("replaced argument changed");
            world.Subject.Rank = 1;
            step("branch taken again");
        }, false);
    }

    [TestMethod]
    public void TheGraphIsStillBuiltWhereTheAnalyzerRefusesTheBody()
    {
        var r = Expression.Parameter(typeof(Recorded), "r");
        var x = Expression.Parameter(typeof(int), "x");
        var body = Expression.Add(Expression.Property(Expression.Call(r, typeof(Recorded).GetMethod(nameof(Recorded.HeldAt))!, x), nameof(Recorded.Rank)), x);
        var held = Expression.Lambda<Func<Recorded, int, int>>(body, r, x);
        AssertAgreement(world => Invoke<int>(held, s => [s, Property(s, nameof(Recorded.Score))]), (world, step) =>
        {
            world.Subject.Score = 3;
            step("argument changed");
            world.Subject.Rank = 4;
            step("held value's property changed");
        }, false);
    }
}
