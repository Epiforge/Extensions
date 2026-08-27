namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

[TestClass]
public class QueryOrderingUnderConcurrency
{
    [TestMethod]
    [Timeout(120000)]
    public void ConcurrentKeyChangesLeaveTheOrderingConsistent()
    {
        var threadCount = Math.Max(8, Environment.ProcessorCount * 2);
        const int peoplePerThread = 12;
        const int iterations = 60;

        var people = Enumerable.Range(0, threadCount * peoplePerThread).Select(index => new TestPerson($"P{index:D4}")).ToList();
        var source = new ObservableRangeCollection<TestPerson>(people);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using (var orderByQuery = sourceQuery.ObserveOrderBy((person => person.Name!.Length, false), (person => person.Name!, false)))
            {
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
                Parallel.For(0, threadCount + 1, new ParallelOptions { MaxDegreeOfParallelism = threadCount + 1 }, worker =>
                {
                    if (worker == threadCount)
                    {
                        for (var iteration = 0; iteration < iterations * 4; ++iteration)
                        {
                            try
                            {
                                var observed = orderByQuery!.ToList();
                                Assert.AreEqual(people.Count, observed.Count);
                            }
                            catch (Exception ex)
                            {
                                recordFault(ex, $"the reader failed on iteration {iteration}: {ex.GetType().Name}: {ex.Message}");
                                return;
                            }
                            Thread.Yield();
                        }
                        return;
                    }
                    var owned = people.Skip(worker * peoplePerThread).Take(peoplePerThread).ToList();
                    for (var iteration = 0; iteration < iterations; ++iteration)
                    {
                        try
                        {
                            for (var i = 0; i < owned.Count; ++i)
                                owned[i].Name = ((iteration + worker) % 3) switch
                                {
                                    0 => $"A{i:D4}",
                                    1 => $"M{i:D2}",
                                    _ => $"Z{i:D6}"
                                };
                        }
                        catch (Exception ex)
                        {
                            recordFault(ex, $"thread {worker} failed on iteration {iteration}: {ex.GetType().Name}: {ex.Message}");
                            return;
                        }
                        if (iteration % 8 == worker % 8)
                            Thread.Yield();
                    }
                });
                Assert.IsNull(fault, faultContext);
                for (var i = 0; i < people.Count; ++i)
                    people[i].Name = $"Z{i:D4}";
                var expected = people.OrderBy(person => person.Name!.Length).ThenBy(person => person.Name!).ToList();
                CollectionAssert.AreEqual(expected, orderByQuery.ToList());
            }
            Assert.AreEqual(0, sourceQuery.CachedObservableQueries);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
