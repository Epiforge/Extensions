namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class SubscriptionAgreement
{
    sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) :
        ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }

    static void AssertAgreement<TResult>(Expression<Func<Recorded, TResult>> lambda, Recorded subject, ExpressionObserverOptions? options = null)
    {
        var log = subject.Log;
        var normalized = new ParameterReplacer(lambda.Parameters[0], Expression.Constant(subject, typeof(Recorded))).Visit(lambda.Body);
        var plan = new Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer(options ?? new ExpressionObserverOptions()).Plan(normalized);
        Assert.IsTrue(plan.IsEligible, plan.ToString());
        var observer = options is null ? new ExpressionObserver() : new ExpressionObserver(options);
        using (observer.Observe(lambda, subject))
        {
            var planned = Planned(plan, log);
            var attached = log.Attachments().Distinct().ToList();
            CollectionAssert.AreEqual(planned.ToArray(), attached.ToArray(), $"plan: [{string.Join(", ", planned)}]; graph: [{string.Join(", ", attached)}]");
        }
        Assert.AreEqual(0, log.Outstanding, "the graph did not detach everything it attached");
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    static string EventNameOf(DirectSubscriptionKind kind) =>
        kind switch
        {
            DirectSubscriptionKind.MemberPropertyChanged or DirectSubscriptionKind.IndexerPropertyChanged => "PropertyChanged",
            DirectSubscriptionKind.DictionaryChanged => "DictionaryChanged",
            _ => "CollectionChanged"
        };

    static IReadOnlyList<string> Planned(DirectSubscriptionPlan plan, SubscriptionLog log)
    {
        var planned = new List<string>();
        for (int i = 0, ii = plan.Subscriptions.Count; i < ii; ++i)
        {
            var subscription = plan.Subscriptions[i];
            var value = Resolve(subscription.Source!);
            if (subscription.ResolveKind(value) is var kind && kind is not DirectSubscriptionKind.None && log.Describe(value!, EventNameOf(kind)) is var described && !planned.Contains(described))
                planned.Add(described);
        }
        planned.Sort(StringComparer.Ordinal);
        return planned;
    }

    static object? Resolve(Expression expression) =>
        expression is ConstantExpression constantExpression ? constantExpression.Value : Expression.Lambda(expression).Compile().DynamicInvoke();

    [TestMethod]
    public void TheGraphDoesNotSubscribeToAnUntakenBranchUntilItIsTaken()
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log);
        var subject = new Recorded(log);
        var observer = new ExpressionObserver();
        using (observer.Observe(s => s.Rank > 0 ? other.Rank : s.Score, subject))
        {
            Assert.AreEqual(2, log.Attachments().Count);
            subject.Rank = 1;
            Assert.AreEqual(3, log.Attachments().Count);
        }
        Assert.AreEqual(0, log.Outstanding);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ClosureFieldHoldingACollection()
    {
        var log = new SubscriptionLog();
        var items = new RecordedCollection(log);
        AssertAgreement(subject => subject.Rank + items.Count, new Recorded(log));
    }

    [TestMethod]
    public void ClosureFieldHoldingSomethingWhichNotifiesBothWays()
    {
        var log = new SubscriptionLog();
        var table = new RecordedTable(log);
        AssertAgreement(subject => subject.Rank + table.Count, new Recorded(log));
    }

    [TestMethod]
    public void ClosureFieldHoldingSomethingWhichNotifiesBothWaysWhenDictionaryChangedIsExcluded()
    {
        var log = new SubscriptionLog();
        var table = new RecordedTable(log);
        AssertAgreement(subject => subject.Rank + table.Count, new Recorded(log), new ExpressionObserverOptions { MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged = false });
    }

    [TestMethod]
    public void IgnoredPropertyChangeNotification()
    {
        var options = new ExpressionObserverOptions();
        options.AddIgnoredPropertyChangeNotification(typeof(Recorded).GetProperty(nameof(Recorded.Rank))!);
        AssertAgreement(subject => subject.Rank * 2, new Recorded(new SubscriptionLog()), options);
    }

    [TestMethod]
    public void MemberOnTheArgument() =>
        AssertAgreement(subject => subject.Rank * 2, new Recorded(new SubscriptionLog()));

    [TestMethod]
    public void MemberOnTheArgumentAndOnAClosure()
    {
        var log = new SubscriptionLog();
        var other = new Recorded(log);
        AssertAgreement(subject => subject.Rank > other.Rank, new Recorded(log));
    }

    [TestMethod]
    public void RepeatedMember() =>
        AssertAgreement(subject => subject.Rank + subject.Rank, new Recorded(new SubscriptionLog()));

    [TestMethod]
    public void TwoMembersOnTheArgument() =>
        AssertAgreement(subject => subject.Rank + subject.Score, new Recorded(new SubscriptionLog()));
}
