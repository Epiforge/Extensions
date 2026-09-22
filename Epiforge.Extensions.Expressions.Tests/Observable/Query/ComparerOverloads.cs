namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers the maximum and minimum taken by a comparer the caller supplies, which is how a consumer compares text under a culture of its choosing rather than the calling thread's
/// </summary>
[TestClass]
public class ComparerOverloads
{
    /// <summary>
    /// Returns the difference of two integers scaled up, which is a valid comparison whose results are not confined to -1, 0 and 1
    /// </summary>
    sealed class ScaledComparer :
        IComparer<int>
    {
        public static ScaledComparer Instance { get; } = new();

        public int Compare(int x, int y) =>
            (x - y) * 5;
    }

    /// <summary>
    /// A value whose own comparison returns the difference scaled up, which <see cref="Comparer{T}.Default"/> passes through unchanged
    /// </summary>
    readonly struct ScaledValue(int value) :
        IComparable<ScaledValue>
    {
        public int Value { get; } = value;

        public int CompareTo(ScaledValue other) =>
            (Value - other.Value) * 5;
    }

    /// <summary>
    /// Two comparers which order <c>"a"</c> and <c>"B"</c> opposite ways, so that an observation which ignored the comparer it was given could not pass
    /// </summary>
    [TestMethod]
    public void TheComparerGivenDecidesTheMaximumAndMinimum()
    {
        var source = new ObservableRangeCollection<string>(["a", "B"]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var ordinalMax = sourceQuery.ObserveMax(StringComparer.Ordinal))
        using (var invariantMax = sourceQuery.ObserveMax(StringComparer.InvariantCulture))
        using (var ordinalMin = sourceQuery.ObserveMin(StringComparer.Ordinal))
        using (var invariantMin = sourceQuery.ObserveMin(StringComparer.InvariantCulture))
        {
            Assert.AreEqual("a", ordinalMax.Evaluation.Result);
            Assert.AreEqual("B", invariantMax.Evaluation.Result);
            Assert.AreEqual("B", ordinalMin.Evaluation.Result);
            Assert.AreEqual("a", invariantMin.Evaluation.Result);
            source.Add("c");
            Assert.AreEqual("c", ordinalMax.Evaluation.Result);
            Assert.AreEqual("c", invariantMax.Evaluation.Result);
            source.Remove("c");
            source.Remove("a");
            Assert.AreEqual("B", ordinalMax.Evaluation.Result);
            Assert.AreEqual("B", invariantMin.Evaluation.Result);
            source.Clear();
            Assert.IsNotNull(ordinalMax.Evaluation.Fault, "the maximum of nothing did not fault");
            Assert.IsNotNull(invariantMin.Evaluation.Fault, "the minimum of nothing did not fault");
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }

    /// <summary>
    /// The selector forms take the comparer for the values the selector produces
    /// </summary>
    [TestMethod]
    public void ASelectorAndAComparerTogether()
    {
        var source = new ObservableRangeCollection<TestMatter>([new("a", 1), new("B", 2)]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var ordinalMax = sourceQuery.ObserveMax(matter => matter.Name, StringComparer.Ordinal))
        using (var ordinalMin = sourceQuery.ObserveMin(matter => matter.Name, StringComparer.Ordinal))
        {
            Assert.AreEqual("a", ordinalMax.Evaluation.Result);
            Assert.AreEqual("B", ordinalMin.Evaluation.Result);
            source[0].Name = "A";
            Assert.AreEqual("B", ordinalMax.Evaluation.Result);
            Assert.AreEqual("A", ordinalMin.Evaluation.Result);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    /// <summary>
    /// Observations with the same comparer share one query, and the overloads without one share the query of the default comparer
    /// </summary>
    [TestMethod]
    public void ObservationsWithTheSameComparerShareAQuery()
    {
        var source = new ObservableRangeCollection<int>([3, 1, 2]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var first = sourceQuery.ObserveMax(ScaledComparer.Instance))
            using (var second = sourceQuery.ObserveMax(ScaledComparer.Instance))
                Assert.AreEqual(1, sourceQuery.CachedObservableQueries, "two observations with the same comparer did not share a query");
            using (var implicitDefault = sourceQuery.ObserveMax())
            using (var explicitDefault = sourceQuery.ObserveMax(Comparer<int>.Default))
                Assert.AreEqual(1, sourceQuery.CachedObservableQueries, "the overload without a comparer did not share the default comparer's query");
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
    }

    /// <summary>
    /// A comparer may return any negative or positive number, not only -1 and 1, and the maximum and minimum must read its sign
    /// </summary>
    [TestMethod]
    public void AComparisonIsReadBySignNotByMagnitude()
    {
        var source = new ObservableRangeCollection<int>([1, 3, 2]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var max = sourceQuery.ObserveMax(ScaledComparer.Instance))
        using (var min = sourceQuery.ObserveMin(ScaledComparer.Instance))
        {
            Assert.AreEqual(3, max.Evaluation.Result);
            Assert.AreEqual(1, min.Evaluation.Result);
            source.Add(7);
            Assert.AreEqual(7, max.Evaluation.Result);
            source.Add(-4);
            Assert.AreEqual(-4, min.Evaluation.Result);
        }
    }

    /// <summary>
    /// The same holds for the overloads without a comparer, over a type whose own comparison returns a magnitude, which the default comparer passes straight through
    /// </summary>
    [TestMethod]
    public void TheDefaultComparisonIsReadBySignNotByMagnitude()
    {
        var source = new ObservableRangeCollection<ScaledValue>([new(1), new(3), new(2)]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var max = sourceQuery.ObserveMax())
        using (var min = sourceQuery.ObserveMin())
        {
            Assert.AreEqual(3, max.Evaluation.Result.Value);
            Assert.AreEqual(1, min.Evaluation.Result.Value);
            source.Add(new(7));
            Assert.AreEqual(7, max.Evaluation.Result.Value);
        }
    }
}
