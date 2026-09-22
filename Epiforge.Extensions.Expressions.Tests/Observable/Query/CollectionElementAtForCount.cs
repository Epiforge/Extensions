namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers the element at a position computed from the count, over an ordering, which is how a median or a percentile is selected
/// </summary>
[TestClass]
public class CollectionElementAtForCount
{
    static readonly Func<int, int> lowerMiddle = count => (count - 1) / 2;
    static readonly Func<int, int> upperMiddle = count => count / 2;

    [TestMethod]
    [Timeout(300000)]
    public void BothMiddlesAreFollowedAcrossRandomOperations()
    {
        for (var seed = 0; seed < 20; ++seed)
        {
            var random = new Random(seed);
            var nextName = 0;
            var source = TestMatterMutations.Create(random, ref nextName);
            var collectionObserver = CollectionObserverHelpers.Create();
            using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
            using (var ordered = sourceQuery.ObserveOrderBy(matter => matter.Hours))
            using (var lower = ordered.ObserveElementAt(lowerMiddle))
            using (var upper = ordered.ObserveElementAt(upperMiddle))
                for (var step = 0; step < 150; ++step)
                {
                    var operation = TestMatterMutations.Mutate(random, source, ref nextName);
                    var context = $"seed {seed}, step {step}, after {operation}";
                    var expected = source.Select(matter => matter.Hours).OrderBy(hours => hours).ToList();
                    if (expected.Count == 0)
                    {
                        Assert.IsInstanceOfType<InvalidOperationException>(lower.Evaluation.Fault, $"{context}: the lower middle of nothing did not fault as a sequence with no elements");
                        Assert.IsInstanceOfType<InvalidOperationException>(upper.Evaluation.Fault, $"{context}: the upper middle of nothing did not fault as a sequence with no elements");
                        continue;
                    }
                    Assert.IsNull(lower.Evaluation.Fault, $"{context}: the lower middle faulted");
                    Assert.IsNull(upper.Evaluation.Fault, $"{context}: the upper middle faulted");
                    Assert.AreEqual(expected[(expected.Count - 1) / 2], lower.Evaluation.Result.Hours, $"{context}: the lower middle diverged");
                    Assert.AreEqual(expected[expected.Count / 2], upper.Evaluation.Result.Hours, $"{context}: the upper middle diverged");
                }
            Assert.AreEqual(0, collectionObserver.CachedObservableQueries, $"seed {seed}: a query was left cached");
        }
    }

    [TestMethod]
    public void AnEmptyCollectionFaultsWithoutAskingForAnIndex()
    {
        var asked = 0;
        Func<int, int> counted = count =>
        {
            ++asked;
            return 0;
        };
        var source = new ObservableRangeCollection<int>();
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var element = sourceQuery.ObserveElementAt(counted))
        {
            Assert.IsInstanceOfType<InvalidOperationException>(element.Evaluation.Fault);
            Assert.AreEqual(0, asked, "the function was asked for an index into an empty collection");
            source.Add(5);
            Assert.IsNull(element.Evaluation.Fault);
            Assert.AreEqual(5, element.Evaluation.Result);
            Assert.AreEqual(1, asked);
        }
    }

    [TestMethod]
    public void AnIndexOutOfRangeFaultsAndAThrowingFunctionFaultsWithWhatItThrew()
    {
        var source = new ObservableRangeCollection<int>([1, 2, 3]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var beyond = sourceQuery.ObserveElementAt(count => count))
        using (var throwing = sourceQuery.ObserveElementAt(count => throw new ArithmeticException()))
        {
            Assert.IsInstanceOfType<IndexOutOfRangeException>(beyond.Evaluation.Fault);
            Assert.IsInstanceOfType<ArithmeticException>(throwing.Evaluation.Fault);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }

    [TestMethod]
    public void ObservationsWithTheSameFunctionShareAQuery()
    {
        var source = new ObservableRangeCollection<int>([1, 2, 3]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var first = sourceQuery.ObserveElementAt(upperMiddle))
            using (var second = sourceQuery.ObserveElementAt(upperMiddle))
                Assert.AreEqual(1, sourceQuery.CachedObservableQueries);
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
    }
}
