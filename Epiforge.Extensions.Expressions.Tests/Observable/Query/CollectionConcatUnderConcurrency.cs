namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class CollectionConcatUnderConcurrency
{
    [TestMethod]
    [Timeout(120000)]
    public void ConcurrentOperandChangesLeaveTheNotificationsConsistent()
    {
        const int iterations = 500;

        var first = new ObservableRangeCollection<int>(Enumerable.Range(0, 20));
        var second = new ObservableRangeCollection<int>(Enumerable.Range(10000, 20));
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var firstQuery = collectionObserver.ObserveReadOnlyList(first))
        using (var secondQuery = collectionObserver.ObserveReadOnlyList(second))
        {
            using (var concatQuery = firstQuery.ObserveConcat(secondQuery))
            {
                var mirror = new List<int>(concatQuery);
                var mirrorFailure = null as string;
                void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                {
                    try
                    {
                        switch (e.Action)
                        {
                            case NotifyCollectionChangedAction.Add:
                                for (var i = 0; i < e.NewItems!.Count; ++i)
                                    mirror.Insert(e.NewStartingIndex + i, (int)e.NewItems[i]!);
                                break;
                            case NotifyCollectionChangedAction.Remove:
                                mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                                break;
                            case NotifyCollectionChangedAction.Replace:
                                mirror.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                                for (var i = 0; i < e.NewItems!.Count; ++i)
                                    mirror.Insert(e.OldStartingIndex + i, (int)e.NewItems[i]!);
                                break;
                            case NotifyCollectionChangedAction.Move:
                                var moved = mirror.GetRange(e.OldStartingIndex, e.OldItems!.Count);
                                mirror.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                                mirror.InsertRange(e.NewStartingIndex, moved);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        mirrorFailure ??= $"{e.Action} at {e.NewStartingIndex}/{e.OldStartingIndex} could not be applied to a mirror of {mirror.Count}: {ex.GetType().Name}";
                    }
                }
                concatQuery.CollectionChanged += collectionChanged;

                var faultAccess = new object();
                Exception? fault = null;
                string? faultContext = null;
                void recordFault(Exception ex, string context)
                {
                    lock (faultAccess)
                        if (fault is null)
                        {
                            fault = ex;
                            faultContext = context;
                        }
                }

                Parallel.For(0, 2, new ParallelOptions { MaxDegreeOfParallelism = 2 }, worker =>
                {
                    var target = worker == 0 ? first : second;
                    var seed = worker == 0 ? 100000 : 200000;
                    try
                    {
                        for (var iteration = 0; iteration < iterations; ++iteration)
                        {
                            target.Add(seed + iteration);
                            target.Insert(0, seed + iteration + 500000);
                            target[1] = seed + iteration + 900000;
                            target.Move(0, target.Count - 1);
                            target.RemoveAt(target.Count - 1);
                            target.RemoveAt(0);
                            Thread.Yield();
                        }
                    }
                    catch (Exception ex)
                    {
                        recordFault(ex, $"worker {worker} failed: {ex.GetType().Name}: {ex.Message}");
                    }
                });

                concatQuery.CollectionChanged -= collectionChanged;
                if (fault is not null)
                    Assert.Fail(faultContext);
                Assert.IsNull(mirrorFailure, mirrorFailure);
                var expected = first.Concat(second).ToList();
                Assert.AreEqual(expected.Count, concatQuery.Count, "the count diverged from the operands");
                CollectionAssert.AreEqual(expected, concatQuery.ToList(), "the concatenation diverged from the operands");
                CollectionAssert.AreEqual(expected, mirror, "the notification mirror diverged from the operands");
            }
            Assert.AreEqual(0, firstQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
