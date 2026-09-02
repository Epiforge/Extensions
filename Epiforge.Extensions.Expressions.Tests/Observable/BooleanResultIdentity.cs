namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class BooleanResultIdentity
{
    static ExpressionObserver Create(bool useDirectSubscription) =>
        new(new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription });

    [TestMethod]
    public void AConjunctionAgreesBetweenMechanisms()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 0, Score = 0 };
        Expression<Func<Recorded, bool>> predicate = s => (s.Rank & 1) == 0 && s.Score > 0;
        AssertMechanismsAgree(log, subject, predicate);
    }

    [TestMethod]
    public void ADisjunctionAgreesBetweenMechanisms()
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 0, Score = 0 };
        Expression<Func<Recorded, bool>> predicate = s => (s.Rank & 1) == 0 || s.Score > 0;
        AssertMechanismsAgree(log, subject, predicate);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ANegatedComparisonIsAnnouncedOnEveryCrossing(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Rank = 0 };
        var observer = Create(useDirectSubscription);
        var announcements = 0;
        using (var expr = observer.ObserveWithoutOptimization(s => !((s.Rank & 1) == 0), subject))
        {
            Assert.AreEqual(false, expr.Evaluation.Result);
            expr.PropertyChanged += (sender, e) => ++announcements;
            for (var rank = 1; rank <= 8; ++rank)
            {
                subject.Rank = rank;
                Assert.AreEqual(rank % 2 != 0, expr.Evaluation.Result);
                Assert.AreEqual(rank, announcements);
            }
        }
        Assert.AreEqual(0, log.Outstanding);
    }

    static void AssertMechanismsAgree(SubscriptionLog log, Recorded subject, Expression<Func<Recorded, bool>> predicate)
    {
        var evaluate = predicate.Compile();
        var directAnnounced = new List<bool>();
        var expected = new List<bool>();
        var graphAnnounced = new List<bool>();
        var graphObserver = Create(false);
        var directObserver = Create(true);
        using (var graphExpression = graphObserver.ObserveWithoutOptimization(predicate, subject))
        using (var directExpression = directObserver.ObserveWithoutOptimization(predicate, subject))
        {
            var previous = evaluate(subject);
            Assert.AreEqual(previous, graphExpression.Evaluation.Result);
            Assert.AreEqual(previous, directExpression.Evaluation.Result);
            graphExpression.PropertyChanged += (sender, e) => graphAnnounced.Add(graphExpression.Evaluation.Result);
            directExpression.PropertyChanged += (sender, e) => directAnnounced.Add(directExpression.Evaluation.Result);
            for (var step = 1; step <= 8; ++step)
            {
                if (step % 2 == 0)
                    subject.Score = step % 4 == 0 ? 1 : 0;
                else
                    subject.Rank = step;
                var current = evaluate(subject);
                if (current != previous)
                    expected.Add(current);
                previous = current;
                Assert.AreEqual(current, graphExpression.Evaluation.Result, $"the graph disagreed with the compiled predicate at step {step}");
                Assert.AreEqual(current, directExpression.Evaluation.Result, $"direct subscription disagreed with the compiled predicate at step {step}");
            }
        }
        Assert.IsTrue(expected.SequenceEqual(graphAnnounced), $"the graph announced [{string.Join(", ", graphAnnounced)}] where the compiled predicate changed to [{string.Join(", ", expected)}]");
        Assert.IsTrue(expected.SequenceEqual(directAnnounced), $"direct subscription announced [{string.Join(", ", directAnnounced)}] where the compiled predicate changed to [{string.Join(", ", expected)}]");
        Assert.AreEqual(0, log.Outstanding);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ATypeTestIsAnnouncedOnEveryCrossing(bool useDirectSubscription)
    {
        var log = new SubscriptionLog();
        var subject = new Recorded(log) { Tag = null };
        var observer = Create(useDirectSubscription);
        var announcements = 0;
        using (var expr = observer.ObserveWithoutOptimization(s => (object?)s.Tag is string, subject))
        {
            Assert.AreEqual(false, expr.Evaluation.Result);
            expr.PropertyChanged += (sender, e) => ++announcements;
            for (var step = 1; step <= 8; ++step)
            {
                subject.Tag = step % 2 != 0 ? "tagged" : null;
                Assert.AreEqual(step % 2 != 0, expr.Evaluation.Result);
                Assert.AreEqual(step, announcements);
            }
        }
        Assert.AreEqual(0, log.Outstanding);
    }
}
