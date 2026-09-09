namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ConstructedNotification
{
    public sealed class Box
    {
        public int First;
        public int[]? Values;

        public Box()
        {
        }

        public Box(int first) =>
            First = first;

        static bool SameValues(int[]? left, int[]? right)
        {
            if (left is null || right is null)
                return left is null && right is null;
            if (left.Length != right.Length)
                return false;
            for (var i = 0; i < left.Length; ++i)
                if (left[i] != right[i])
                    return false;
            return true;
        }

        public override bool Equals(object? obj) =>
            obj is Box other && First == other.First && SameValues(Values, other.Values);

        public override int GetHashCode() =>
            HashCode.Combine(First, Values is null ? -1 : Values.Length);
    }

    public sealed class Counted :
        PropertyChangeNotifier
    {
        int rank;

        public int Rank
        {
            get => rank;
            set => SetBackedProperty(ref rank, in value);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AConstructorArgument(bool useDirectSubscription) =>
        Assert.AreEqual(1, AnnouncementsForOneChange(useDirectSubscription, s => new Box(s.Rank)));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AMemberInitializerBinding(bool useDirectSubscription) =>
        Assert.AreEqual(1, AnnouncementsForOneChange(useDirectSubscription, s => new Box { First = s.Rank }));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AMemberInitializerDoesNotMutateAResultItAlreadyProduced(bool useDirectSubscription)
    {
        var subject = new Counted { Rank = 1 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(s => new Box { First = s.Rank }, subject);
        var first = (Box)expression.Evaluation.Result!;
        subject.Rank = 2;
        var second = (Box)expression.Evaluation.Result!;
        Assert.AreNotSame(first, second, "an evaluation reused the instance a previous one produced");
        Assert.AreEqual(1, first.First, "a result already handed to a consumer was mutated after the fact");
        Assert.AreEqual(2, second.First);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnArrayInitializerBoundIntoAMemberInitializer(bool useDirectSubscription) =>
        Assert.AreEqual(1, AnnouncementsForOneChange(useDirectSubscription, s => new Box { Values = new[] { s.Rank } }));

    /// <summary>
    /// Counts what an observation announces when an argument it reads changes exactly once
    /// </summary>
    /// <remarks>
    /// The cache check guards the direct row against becoming a second graph row: a direct observation is not cached, so an analyzer which quietly declined the shape is caught here rather than passing vacuously. The graph row needs no such guard because the option is refused at the entry point
    /// </remarks>
    static int AnnouncementsForOneChange(bool useDirectSubscription, Expression<Func<Counted, object?>> lambda)
    {
        var subject = new Counted { Rank = 1 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var announcements = 0;
        using (var expression = observer.Observe(lambda, subject))
        {
            expression.PropertyChanged += (sender, e) => ++announcements;
            Assert.IsNull(expression.Evaluation.Fault);
            if (useDirectSubscription)
                Assert.AreEqual(0, observer.CachedObservableExpressions, "the analyzer declined the shape, so this row measured the graph");
            subject.Rank = 2;
        }
        return announcements;
    }
}
