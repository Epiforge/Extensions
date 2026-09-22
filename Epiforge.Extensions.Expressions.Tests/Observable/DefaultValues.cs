namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Covers expressions containing a <see cref="DefaultExpression"/>, which a formula engine emits for a branch that supplies no value of its own, such as a switch with no trailing default
/// </summary>
/// <remarks>
/// C# never emits one for <c>default(T)</c>, which it compiles to a constant, so every expression here is built by hand. Each is observed on both mechanisms with and without the optimizer, because the optimizer runs before parameter replacement and before the analysis and could rewrite the node out of the tree before either sees it; the rows without it are the ones which say what the library itself does
/// </remarks>
[TestClass]
public class DefaultValues
{
    static readonly ParameterExpression recorded = Expression.Parameter(typeof(Recorded), "r");
    static readonly MemberExpression rank = Expression.Property(recorded, nameof(Recorded.Rank));

    static ExpressionObserver Create(bool useDirectSubscription, bool optimize)
    {
        var options = new ExpressionObserverOptions { UseDirectSubscription = useDirectSubscription };
        if (optimize)
            options.Optimizer = ExpressionOptimizer.tryVisit;
        return new(options);
    }

    static Expression<Func<Recorded, TResult>> Lambda<TResult>(Expression body) =>
        Expression.Lambda<Func<Recorded, TResult>>(body, recorded);

    /// <summary>
    /// Observes one expression on both mechanisms, applies each change to both subjects in turn, and requires the two to agree with each other and with the expected value at every step, on what they subscribe to, and on how often they announce
    /// </summary>
    static void AssertObserved<TResult>(Expression<Func<Recorded, TResult>> form, bool optimize, Action<Recorded> prepare, TResult initially, params (Action<Recorded> change, TResult expected)[] steps)
    {
        var graphLog = new SubscriptionLog();
        var graphSubject = new Recorded(graphLog);
        prepare(graphSubject);
        var fastLog = new SubscriptionLog();
        var fastSubject = new Recorded(fastLog);
        prepare(fastSubject);
        var graphObserver = Create(false, optimize);
        var fastObserver = Create(true, optimize);
        var graphAnnouncements = 0;
        var fastAnnouncements = 0;
        using (var graph = graphObserver.Observe(form, graphSubject))
        using (var fast = fastObserver.Observe(form, fastSubject))
        {
            graph.PropertyChanged += (sender, e) => ++graphAnnouncements;
            fast.PropertyChanged += (sender, e) => ++fastAnnouncements;
            Assert.AreEqual(0, fastObserver.CachedObservableExpressions, "the analyzer refused an expression whose only unusual node is a default value");
            Assert.IsNull(graph.Evaluation.Fault, "the graph faulted before anything changed");
            Assert.IsNull(fast.Evaluation.Fault, "the fast path faulted before anything changed");
            Assert.AreEqual(initially, graph.Evaluation.Result, "the graph's value before anything changed");
            Assert.AreEqual(initially, fast.Evaluation.Result, "the fast path's value before anything changed");
            CollectionAssert.AreEqual(graphLog.Attachments().ToArray(), fastLog.Attachments().ToArray(), $"graph: [{string.Join(", ", graphLog.Attachments())}]; fast: [{string.Join(", ", fastLog.Attachments())}]");
            for (var i = 0; i < steps.Length; ++i)
            {
                var (change, expected) = steps[i];
                change(graphSubject);
                change(fastSubject);
                Assert.AreEqual(expected, graph.Evaluation.Result, $"the graph's value after change {i + 1}");
                Assert.AreEqual(expected, fast.Evaluation.Result, $"the fast path's value after change {i + 1}");
                Assert.AreEqual(graphAnnouncements, fastAnnouncements, $"the mechanisms did not announce alike after change {i + 1}");
                CollectionAssert.AreEqual(graphLog.Attachments().ToArray(), fastLog.Attachments().ToArray(), $"after change {i + 1}");
            }
        }
        Assert.AreEqual(0, graphLog.Outstanding, "the graph did not detach everything it attached");
        Assert.AreEqual(0, fastLog.Outstanding, "the fast path did not detach everything it attached");
    }

    /// <summary>
    /// The shape GS5's <c>Switch</c> builds when given no trailing default: nested conditionals ending in the default of the result type, observed while the conditions move into and out of it
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ASwitchWithNoTrailingDefaultEvaluatesToTheDefaultWhenNoConditionHolds(bool optimize) =>
        AssertObserved(
            Lambda<int>(Expression.Condition(Expression.GreaterThan(rank, Expression.Constant(10)), Expression.Constant(1), Expression.Condition(Expression.GreaterThan(rank, Expression.Constant(5)), Expression.Constant(2), Expression.Default(typeof(int))))),
            optimize,
            r => r.Rank = 0,
            0,
            (r => r.Rank = 7, 2),
            (r => r.Rank = 20, 1),
            (r => r.Rank = 0, 0));

    /// <summary>
    /// The default of a reference type, which is null, reached and left again
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TheDefaultOfAReferenceTypeIsNull(bool optimize) =>
        AssertObserved(
            Lambda<string?>(Expression.Condition(Expression.GreaterThan(rank, Expression.Constant(0)), Expression.Property(recorded, nameof(Recorded.Tag)), Expression.Default(typeof(string)))),
            optimize,
            r =>
            {
                r.Rank = 0;
                r.Tag = "tagged";
            },
            null,
            (r => r.Rank = 1, "tagged"),
            (r => r.Rank = 0, null));

    /// <summary>
    /// A default value which is the whole of the expression, which never changes and subscribes to nothing
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ADefaultValueAloneIsObserved(bool optimize) =>
        AssertObserved(
            Lambda<int>(Expression.Default(typeof(int))),
            optimize,
            r => r.Rank = 0,
            0,
            (r => r.Rank = 3, 0));

    /// <summary>
    /// A default value as the argument of a call whose value the observer disposes of, through an object which cannot change, which makes the call invariant: the graph makes it once, and the fast path must recognize it as invariant and hold it rather than refuse it
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ADefaultValueAsTheArgumentOfAnInvariantCallLeavesItInvariant(bool optimize)
    {
        var anchor = new Recorded(new SubscriptionLog()) { Rank = 4 };
        var heldAt = Expression.Call(Expression.Constant(anchor), typeof(Recorded).GetMethod(nameof(Recorded.HeldAt))!, Expression.Default(typeof(int)));
        AssertObserved(
            Lambda<int>(Expression.Add(rank, Expression.Property(heldAt, nameof(Recorded.Rank)))),
            optimize,
            r => r.Rank = 1,
            5,
            (r => r.Rank = 2, 6));
    }

    /// <summary>
    /// A default value observed without an argument, which only the graph serves
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ADefaultValueIsObservedWithoutAnArgument(bool optimize)
    {
        var observer = Create(false, optimize);
        using var expression = observer.Observe(Expression.Lambda<Func<int>>(Expression.Condition(Expression.Constant(false), Expression.Constant(1), Expression.Default(typeof(int)))));
        Assert.IsNull(expression.Evaluation.Fault);
        Assert.AreEqual(0, expression.Evaluation.Result);
    }
}
