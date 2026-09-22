namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers the element with the greatest or least key, with and without a comparer, across every kind of change
/// </summary>
/// <remarks>
/// Which of several tied elements is returned is unspecified, so these require only that the element returned is in the collection and that its key is the extreme one
/// </remarks>
[TestClass]
public class CollectionMaxByMinBy
{
    /// <summary>
    /// Orders keys of hours and name by hours and breaks ties by name ordinally, reversed, which is a comparer a caller supplies to make the answer deterministic
    /// </summary>
    /// <remarks>
    /// It compares keys the selector produced and nothing else. An earlier form compared the elements themselves, reading their hours from outside any observation, and the key selector returned the element, which never changes; so a change of hours reached no observation and the extreme was never re-read. That is the contract rather than a defect, and the documentation of the comparer overloads now says so
    /// </remarks>
    sealed class NameBreaksTies :
        IComparer<(int Hours, string Name)>
    {
        public static NameBreaksTies Instance { get; } = new();

        public int Compare((int Hours, string Name) x, (int Hours, string Name) y) =>
            x.Hours != y.Hours ? x.Hours.CompareTo(y.Hours) : string.CompareOrdinal(y.Name, x.Name);
    }

    static void AssertExtreme(ObservableRangeCollection<TestMatter> source, IObservableScalarQuery<TestMatter> max, IObservableScalarQuery<TestMatter> min, string context)
    {
        if (source.Count == 0)
        {
            Assert.IsNotNull(max.Evaluation.Fault, $"{context}: the maximum of nothing did not fault");
            Assert.IsNotNull(min.Evaluation.Fault, $"{context}: the minimum of nothing did not fault");
            return;
        }
        Assert.IsNull(max.Evaluation.Fault, $"{context}: the maximum faulted");
        Assert.IsNull(min.Evaluation.Fault, $"{context}: the minimum faulted");
        CollectionAssert.Contains(source, max.Evaluation.Result, $"{context}: the maximum is not in the collection");
        CollectionAssert.Contains(source, min.Evaluation.Result, $"{context}: the minimum is not in the collection");
        Assert.AreEqual(source.Max(matter => matter.Hours), max.Evaluation.Result.Hours, $"{context}: the maximum's key is not the greatest");
        Assert.AreEqual(source.Min(matter => matter.Hours), min.Evaluation.Result.Hours, $"{context}: the minimum's key is not the least");
    }

    [TestMethod]
    [Timeout(300000)]
    public void TheExtremeElementIsFollowedAcrossRandomOperations()
    {
        for (var seed = 0; seed < 20; ++seed)
        {
            var random = new Random(seed);
            var nextName = 0;
            var source = TestMatterMutations.Create(random, ref nextName);
            var collectionObserver = CollectionObserverHelpers.Create();
            using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
            using (var max = sourceQuery.ObserveMaxBy(matter => matter.Hours))
            using (var min = sourceQuery.ObserveMinBy(matter => matter.Hours))
            {
                AssertExtreme(source, max, min, $"seed {seed}, before any change");
                for (var step = 0; step < 150; ++step)
                {
                    var operation = TestMatterMutations.Mutate(random, source, ref nextName);
                    AssertExtreme(source, max, min, $"seed {seed}, step {step}, after {operation}");
                }
            }
            Assert.AreEqual(0, collectionObserver.CachedObservableQueries, $"seed {seed}: a query was left cached");
            Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions, $"seed {seed}: an expression was left cached");
        }
    }

    /// <summary>
    /// A comparer which breaks ties makes the answer the one LINQ gives with the same comparer, exactly
    /// </summary>
    [TestMethod]
    [Timeout(300000)]
    public void AComparerWhichBreaksTiesDecidesTheElement()
    {
        for (var seed = 0; seed < 20; ++seed)
        {
            var random = new Random(seed);
            var nextName = 0;
            var source = TestMatterMutations.Create(random, ref nextName);
            var collectionObserver = CollectionObserverHelpers.Create();
            using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
            using (var max = sourceQuery.ObserveMaxBy(matter => new ValueTuple<int, string>(matter.Hours, matter.Name), NameBreaksTies.Instance))
            using (var min = sourceQuery.ObserveMinBy(matter => new ValueTuple<int, string>(matter.Hours, matter.Name), NameBreaksTies.Instance))
                for (var step = 0; step < 150; ++step)
                {
                    var operation = TestMatterMutations.Mutate(random, source, ref nextName);
                    if (source.Count == 0)
                        continue;
                    var context = $"seed {seed}, step {step}, after {operation}";
                    Assert.AreSame(source.MaxBy(matter => (matter.Hours, matter.Name), NameBreaksTies.Instance), max.Evaluation.Result, $"{context}: the maximum diverged");
                    Assert.AreSame(source.MinBy(matter => (matter.Hours, matter.Name), NameBreaksTies.Instance), min.Evaluation.Result, $"{context}: the minimum diverged");
                }
            Assert.AreEqual(0, collectionObserver.CachedObservableQueries, $"seed {seed}: a query was left cached");
        }
    }

    /// <summary>
    /// A comparer of keys which orders text ordinally, against one which orders it by the invariant culture, over <c>"a"</c> and <c>"B"</c>
    /// </summary>
    [TestMethod]
    public void TheKeyComparerGivenDecidesTheElement()
    {
        var lower = new TestMatter("a", 0);
        var upper = new TestMatter("B", 0);
        var source = new ObservableRangeCollection<TestMatter>([lower, upper]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var ordinal = sourceQuery.ObserveMaxBy(matter => matter.Name, StringComparer.Ordinal))
        using (var invariant = sourceQuery.ObserveMaxBy(matter => matter.Name, StringComparer.InvariantCulture))
        {
            Assert.AreSame(lower, ordinal.Evaluation.Result);
            Assert.AreSame(upper, invariant.Evaluation.Result);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }
}
