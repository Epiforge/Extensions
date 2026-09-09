namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// What a propagation holds together, and what it does not
/// </summary>
/// <remarks>
/// A propagation belongs to the thread rather than to whoever opened it: <c>PropagationScope</c> keeps its depth and its list of pending observations in thread static fields, and only the outermost scope flushes them. What it holds back is an observation's notification, so that no consumer is handed a value which was never simultaneously true of that observation's own inputs. It does not bring one observation up to date before another announces, and it could not: an observation reports the value it last raised, so one which has not been raised yet necessarily reports its previous one.
/// The first form of this class asserted the opposite of that second paragraph on all four of its rows and failed on all four, including both of its controls. The rows below pin what is true instead, because the difference decides what a single observation served by both mechanisms at once would have to preserve: the first guarantee, which is about one observation's inputs, and not the second, which was never offered
/// </remarks>
[TestClass]
public class PropagationOrdering
{
    /// <summary>
    /// Changes two of its members and announces once, naming no property, which is how an object tells an observation that everything it read may have moved
    /// </summary>
    public sealed class Paired :
        INotifyPropertyChanged
    {
        int first = 1;
        int second = 10;

        public int First =>
            first;

        public int Second =>
            second;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Move(int toFirst, int toSecond)
        {
            first = toFirst;
            second = toSecond;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    static List<int> AnnouncedWhileMoving(bool useDirectSubscription)
    {
        var subject = new Paired();
        var announced = new List<int>();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using var expression = observer.Observe(s => s.First + s.Second, subject);
        Assert.AreEqual(11, expression.Evaluation.Result, "the two forms did not start alike");
        expression.PropertyChanged += (sender, e) => announced.Add(expression.Evaluation.Result);
        subject.Move(2, 20);
        return announced;
    }

    /// <summary>
    /// An observation reading two members of one object which moves both and announces once, where 12 and 21 are values the object was never in
    /// </summary>
    /// <remarks>
    /// This is the guarantee a propagation exists for, and it is the one a single observation partitioned across both mechanisms would have to keep. Every graph node once opened a propagation as its first act, so one event two nodes cared about produced two propagations, each flushing before the later node had been told, and this shape announced the new first member added to the stale second
    /// </remarks>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObservationNeverAnnouncesAValueItsSourceWasNeverIn(bool useDirectSubscription)
    {
        var announced = AnnouncedWhileMoving(useDirectSubscription);
        CollectionAssert.DoesNotContain(announced, 12, "the observation announced its new first member added to its stale second");
        CollectionAssert.DoesNotContain(announced, 21, "the observation announced its stale first member added to its new second");
        CollectionAssert.AreEqual(new[] { 22 }, announced, $"the observation announced [{string.Join(", ", announced)}] where one announcement of 22 was owed");
    }

    /// <summary>
    /// Records that an observation reports the value it last raised, so a consumer reading one inside another's announcement is handed the older value
    /// </summary>
    /// <remarks>
    /// This is by design and not a defect, and it is written down because it was mistaken for one. A scoped observation keeps the evaluation it last announced in a field and answers from that field; the field is assigned by its own raise. Two observations enlisted in one propagation are raised one after another, so while the first is raising, the second has not yet assigned its field. A propagation makes each observation's own value consistent; it does not make a set of them consistent with each other, and nothing in the library ever offered that
    /// </remarks>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnObservationReadInsideAnothersAnnouncementReportsWhatItLastRaised(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 1 };
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var announcing = observer.Observe(s => s.Rank, subject))
        using (var read = observer.Observe(s => s.Rank + 0, subject))
        {
            object? during = null;
            announcing.PropertyChanged += (sender, e) => during ??= read.Evaluation.Result;
            subject.Rank = 9;
            Assert.AreEqual(1, during, "an observation reported something other than the value it last raised to a consumer handling another's announcement");
            Assert.AreEqual(9, read.Evaluation.Result, "the observation which was read never caught up once the propagation was over");
        }
        Assert.AreEqual(0, log.Outstanding);
    }
}
