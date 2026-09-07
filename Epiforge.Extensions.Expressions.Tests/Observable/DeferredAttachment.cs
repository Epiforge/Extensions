namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class DeferredAttachment
{
    static readonly PropertyInfo rank = typeof(Recorded).GetProperty(nameof(Recorded.Rank))!;

    static ExpressionObserver Graph() =>
        new(new ExpressionObserverOptions { UseDirectSubscription = false });

    static string Describe(SubscriptionLog log) =>
        $"graph: [{string.Join(", ", log.Attachments())}]";

    [TestMethod]
    public void TheGraphAttachesToTheRightOperandOfCoalesceOnlyWhenTheLeftIsNull()
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log) { Tag = "o" };
        var subject = new Recorded(log) { Tag = "s" };
        var observer = Graph();
        using (observer.Observe(s => s.Tag ?? other.Tag, subject))
        {
            Assert.AreEqual(1, log.Attachments().Count, $"the graph attached to the right operand before the left was null; {Describe(log)}");
            subject.Tag = null;
            Assert.AreEqual(2, log.Attachments().Count, $"the graph did not attach to the right operand once the left became null; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphAttachesToTheRightOperandOfOrElseOnlyWhenTheLeftIsFalse()
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log);
        var subject = new Recorded(log) { Rank = 1 };
        var observer = Graph();
        using (observer.Observe(s => s.Rank > 0 || other.Rank > 0, subject))
        {
            Assert.AreEqual(1, log.Attachments().Count, $"the graph attached to the right operand before the left was false; {Describe(log)}");
            subject.Rank = 0;
            Assert.AreEqual(2, log.Attachments().Count, $"the graph did not attach to the right operand once the left became false; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphAttachesToTheOtherBranchOfAConditionalOnlyWhenTheTestTurns()
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log) { Rank = 9 };
        var subject = new Recorded(log) { Score = 3 };
        var observer = Graph();
        using (var expr = observer.Observe(s => s.Rank > 0 ? other.Rank : s.Score, subject))
        {
            Assert.AreEqual(3, expr.Evaluation.Result);
            Assert.AreEqual(1, log.Attachments().Count, $"the graph attached to the untaken branch; {Describe(log)}");
            subject.Rank = 1;
            Assert.AreEqual(9, expr.Evaluation.Result);
            Assert.AreEqual(2, log.Attachments().Count, $"the graph did not attach to the branch once it was taken; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphKeepsItsSubscriptionToAConditionalBranchAfterItStopsBeingTaken()
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log) { Rank = 9 };
        var subject = new Recorded(log) { Score = 3 };
        var observer = Graph();
        using (observer.Observe(s => s.Rank > 0 ? other.Rank : s.Score, subject))
        {
            subject.Rank = 1;
            Assert.AreEqual(2, log.Outstanding, Describe(log));
            subject.Rank = 0;
            Assert.AreEqual(2, log.Outstanding, $"the graph detached from the branch when it stopped being taken; {Describe(log)}");
            Assert.AreEqual(2, log.Attachments().Count, $"the graph attached to something again; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphsSourcesForAConditionalOverOneObjectDoNotChangeWhenTheOtherBranchIsTaken()
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log) { Rank = 9, Score = 4 };
        var subject = new Recorded(log);
        var observer = Graph();
        using (var expr = observer.Observe(s => s.Rank > 0 ? other.Rank : other.Score, subject))
        {
            var notifications = 0;
            expr.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(IObservableExpression<object?>.Evaluation))
                    ++notifications;
            };
            var beforeTaken = log.Attachments().Distinct().ToList();
            Assert.AreEqual(2, log.Attachments().Count, Describe(log));
            subject.Rank = 1;
            var afterTaken = log.Attachments().Distinct().ToList();
            CollectionAssert.AreEqual(beforeTaken.ToArray(), afterTaken.ToArray(), $"before: [{string.Join(", ", beforeTaken)}]; after: [{string.Join(", ", afterTaken)}]");
            other.Rank = 11;
            Assert.AreEqual(11, expr.Evaluation.Result);
            Assert.AreEqual(2, notifications, "the taken branch was not observed after the test turned");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphAttachesToANestedDeferredOperandOnlyWhenBothBranchesAreTaken()
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log);
        var third = new Recorded(log) { Rank = 1 };
        var subject = new Recorded(log);
        var observer = Graph();
        using (observer.Observe(s => s.Rank > 0 && (other.Rank > 0 && third.Rank > 0), subject))
        {
            Assert.AreEqual(1, log.Attachments().Count, Describe(log));
            subject.Rank = 1;
            Assert.AreEqual(2, log.Attachments().Count, $"the graph attached past the operand which was false; {Describe(log)}");
            other.Rank = 1;
            Assert.AreEqual(3, log.Attachments().Count, $"the graph did not attach to the innermost operand once it was reached; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphAttachesToAConstantsContentsEvenWhereOnlyADeferredBranchReadsIt()
    {
        var log = new SubscriptionLog();
        var flag = new Recorded(log);
        var items = new RecordedCollection(log);
        var observer = new ExpressionObserver(new ExpressionObserverOptions { ConstantExpressionsListenForCollectionChanged = true, UseDirectSubscription = false });
        using (observer.Observe(c => flag.Rank > 0 ? c.Count : 0, items))
        {
            CollectionAssert.Contains(log.Attachments().ToArray(), log.Describe(items, "CollectionChanged"), $"the graph deferred the contents subscription of a constant; {Describe(log)}");
            Assert.AreEqual(2, log.Attachments().Count, Describe(log));
            flag.Rank = 1;
            Assert.AreEqual(3, log.Attachments().Count, $"the graph did not attach to the member once the branch was taken; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphAttachesToAClosureFieldsContentsOnlyWhenTheBranchReadingItIsTaken()
    {
        var log = new SubscriptionLog();
        var items = new RecordedCollection(log);
        var subject = new Recorded(log);
        var observer = new ExpressionObserver(new ExpressionObserverOptions { MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged = true, UseDirectSubscription = false });
        using (observer.Observe(s => s.Rank > 0 ? items.Count : 0, subject))
        {
            CollectionAssert.DoesNotContain(log.Attachments().ToArray(), log.Describe(items, "CollectionChanged"), $"the graph attached the contents of a closure field before the branch reading it was taken; {Describe(log)}");
            Assert.AreEqual(1, log.Attachments().Count, Describe(log));
            subject.Rank = 1;
            CollectionAssert.Contains(log.Attachments().ToArray(), log.Describe(items, "CollectionChanged"), $"the graph did not attach the contents of a closure field once the branch reading it was taken; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void TheGraphAttachesASharedMemberAtItsEarliestUseWhicheverBranchNamesItFirst()
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log) { Rank = 5 };
        var subject = new Recorded(log);
        var otherRank = Expression.MakeMemberAccess(Expression.Constant(other, typeof(Recorded)), rank);
        var subjectRank = Expression.MakeMemberAccess(Expression.Constant(subject, typeof(Recorded)), rank);
        var body = Expression.Add(Expression.Condition(Expression.GreaterThan(subjectRank, Expression.Constant(0)), otherRank, Expression.Constant(0)), otherRank);
        var observer = Graph();
        using (var expr = observer.Observe(Expression.Lambda<Func<int>>(body)))
        {
            Assert.AreEqual(5, expr.Evaluation.Result);
            Assert.AreEqual(2, log.Attachments().Count, $"the graph deferred a member which is also read outside the branch naming it; {Describe(log)}");
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }
}
