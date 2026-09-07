namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class OptimizerAgreement
{
    static string Describe<TResult>(IObservableExpression<Recorded, TResult> expression) =>
        expression.Evaluation.Fault is { } fault ? $"!{fault.GetType().Name}" : expression.Evaluation.Result?.ToString() ?? "<null>";

    /// <summary>
    /// Observes one expression under both mechanisms with the same options, which must agree whether or not an optimizer is rewriting what the graph evaluates
    /// </summary>
    static void AssertMechanismsAgree<TResult>(Expression<Func<Recorded, TResult>> lambda, Func<SubscriptionLog, Recorded> build, bool optimizing)
    {
        if (optimizing)
            Assert.AreNotEqual(lambda.ToString(), ExpressionOptimizer.tryVisit(lambda).ToString(), $"the optimizer left {lambda} alone, so this comparison cannot say anything");
        var graphLog = new SubscriptionLog();
        var graphObserver = new ExpressionObserver(new ExpressionObserverOptions { Optimizer = optimizing ? ExpressionOptimizer.tryVisit : null, UseDirectSubscription = false });
        var fastLog = new SubscriptionLog();
        var fastObserver = new ExpressionObserver(new ExpressionObserverOptions { Optimizer = optimizing ? ExpressionOptimizer.tryVisit : null });
        using (var graphExpression = graphObserver.Observe(lambda, build(graphLog)))
        using (var fastExpression = fastObserver.Observe(lambda, build(fastLog)))
        {
            Assert.AreEqual(0, fastObserver.CachedObservableExpressions, $"the fast path declined {lambda}, which makes this comparison vacuous");
            Assert.AreEqual(Describe(graphExpression), Describe(fastExpression), $"the two mechanisms evaluated {lambda} differently");
            CollectionAssert.AreEqual(graphLog.Attachments().Distinct().ToArray(), fastLog.Attachments().Distinct().ToArray(), $"graph: [{string.Join(", ", graphLog.Attachments())}]; fast: [{string.Join(", ", fastLog.Attachments())}]");
        }
        Assert.AreEqual(0, graphLog.Outstanding);
        Assert.AreEqual(0, fastLog.Outstanding);
    }

    [TestMethod]
    public void AnOperandTheOptimizerRemovesIsNotEvaluatedByEitherMechanism() =>
        AssertMechanismsAgree(s => false & s.Tag!.Length > 0, log => new Recorded(log), true);

    [TestMethod]
    public void AnOperandTheOptimizerRemovesFromADisjunctionIsNotEvaluatedByEitherMechanism() =>
        AssertMechanismsAgree(s => true | s.Tag!.Length > 0, log => new Recorded(log), true);

    [TestMethod]
    public void BothMechanismsEvaluateTheWholeExpressionWhenNoOptimizerIsConfigured() =>
        AssertMechanismsAgree(s => false & s.Tag!.Length > 0, log => new Recorded(log), false);

    [TestMethod]
    public void NeitherMechanismSubscribesToAnOperandTheOptimizerRemoves() =>
        AssertMechanismsAgree(s => false & s.Rank > 0, log => new Recorded(log) { Rank = 2 }, true);
}
