namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers the counts of elements ahead of an element and level with it under an ordering, from which both of Excel's ranks follow
/// </summary>
[TestClass]
public class CollectionRank
{
    static (int Ahead, int Tied) Expected(IReadOnlyList<TestMatter> source, TestMatter element, bool isDescending)
    {
        var ahead = 0;
        var tied = 0;
        foreach (var other in source)
        {
            var comparison = other.Hours.CompareTo(element.Hours) * (isDescending ? -1 : 1);
            if (comparison < 0)
                ++ahead;
            else if (comparison == 0)
                ++tied;
        }
        return (ahead, tied);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(300000)]
    public void RanksAreFollowedAcrossRandomOperations(bool isDescending)
    {
        for (var seed = 0; seed < 20; ++seed)
        {
            var random = new Random(seed);
            var nextName = 0;
            var source = TestMatterMutations.Create(random, ref nextName);
            var tracked = source.Take(3).ToList();
            var collectionObserver = CollectionObserverHelpers.Create();
            using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
            {
                var ranks = tracked.Select(element => sourceQuery.ObserveRank(element, matter => matter.Hours, isDescending)).ToList();
                try
                {
                    for (var step = 0; step < 150; ++step)
                    {
                        var operation = TestMatterMutations.Mutate(random, source, ref nextName);
                        for (var i = 0; i < tracked.Count; ++i)
                        {
                            var context = $"seed {seed}, step {step}, after {operation}, rank of {tracked[i]}";
                            if (source.Contains(tracked[i]))
                            {
                                Assert.IsNull(ranks[i].Evaluation.Fault, $"{context}: faulted");
                                Assert.AreEqual(Expected(source, tracked[i], isDescending), ranks[i].Evaluation.Result, $"{context}: diverged");
                            }
                            else
                                Assert.IsInstanceOfType<InvalidOperationException>(ranks[i].Evaluation.Fault, $"{context}: did not fault while the element was absent");
                        }
                    }
                }
                finally
                {
                    foreach (var rank in ranks)
                        rank.Dispose();
                }
                Assert.AreEqual(0, sourceQuery.CachedObservableQueries, $"seed {seed}: a query was left cached");
            }
        }
    }

    /// <summary>
    /// Keys 1, 2 and 3; the 2 becomes a 3, which moves nothing in the ordering and makes the last two level with each other
    /// </summary>
    [TestMethod]
    public void AKeyChangeWhichMovesNothingStillChangesTheTies()
    {
        var one = new TestMatter("one", 1);
        var two = new TestMatter("two", 2);
        var three = new TestMatter("three", 3);
        var source = new ObservableRangeCollection<TestMatter>([one, two, three]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var ordered = sourceQuery.ObserveOrderBy(matter => matter.Hours))
        using (var twoRank = sourceQuery.ObserveRank(two, matter => matter.Hours, false))
        using (var threeRank = sourceQuery.ObserveRank(three, matter => matter.Hours, false))
        {
            var moves = 0;
            ordered.CollectionChanged += (sender, e) =>
            {
                if (e.Action is NotifyCollectionChangedAction.Move)
                    ++moves;
            };
            Assert.AreEqual((1, 1), twoRank.Evaluation.Result);
            Assert.AreEqual((2, 1), threeRank.Evaluation.Result);
            two.Hours = 3;
            Assert.AreEqual(0, moves, "the ordering moved an element, so this does not test what it is named for");
            Assert.AreEqual((1, 2), twoRank.Evaluation.Result, "the changed element's tie count did not follow its key");
            Assert.AreEqual((1, 2), threeRank.Evaluation.Result, "the unchanged element's tie count did not follow its neighbor's key");
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
    }

    /// <summary>
    /// An element occurring twice is counted twice, in the counts of others and in its own tie count
    /// </summary>
    [TestMethod]
    public void AnElementOccurringTwiceIsCountedTwice()
    {
        var low = new TestMatter("low", 1);
        var high = new TestMatter("high", 5);
        var source = new ObservableRangeCollection<TestMatter>([low, high, low]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var lowRank = sourceQuery.ObserveRank(low, matter => matter.Hours, false))
        using (var highRank = sourceQuery.ObserveRank(high, matter => matter.Hours, false))
        {
            Assert.AreEqual((0, 2), lowRank.Evaluation.Result);
            Assert.AreEqual((2, 1), highRank.Evaluation.Result);
            source.RemoveAt(2);
            Assert.AreEqual((0, 1), lowRank.Evaluation.Result);
            Assert.AreEqual((1, 1), highRank.Evaluation.Result);
        }
    }

    /// <summary>
    /// A rank shares the ordering an observation of the same key and direction already holds, and releases it when it is disposed
    /// </summary>
    [TestMethod]
    public void ARankSharesTheOrderingForTheSameKey()
    {
        var matter = new TestMatter("only", 1);
        var source = new ObservableRangeCollection<TestMatter>([matter]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var ordered = sourceQuery.ObserveOrderBy(m => m.Hours, false))
            {
                var withOrderingAlone = sourceQuery.CachedObservableQueries;
                using (var rank = sourceQuery.ObserveRank(matter, m => m.Hours, false))
                    Assert.AreEqual(withOrderingAlone + 1, sourceQuery.CachedObservableQueries, "the rank built an ordering of its own rather than sharing the one already held");
                Assert.AreEqual(withOrderingAlone, sourceQuery.CachedObservableQueries);
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
    }

    /// <summary>
    /// Two keys, the second breaking ties in the first, so that only elements level on both are counted as tied
    /// </summary>
    [TestMethod]
    public void OnlyElementsLevelOnEveryKeyAreTied()
    {
        var a = new TestMatter("a", 1);
        var b = new TestMatter("b", 1);
        var c = new TestMatter("b", 1);
        var source = new ObservableRangeCollection<TestMatter>([a, b, c]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        using (var bRank = sourceQuery.ObserveRank(b, (m => m.Hours, false), (m => m.Name, false)))
        {
            Assert.AreEqual((1, 2), bRank.Evaluation.Result);
            a.Name = "c";
            Assert.AreEqual((0, 2), bRank.Evaluation.Result);
        }
    }
}
