namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Queries and expressions over two groupings of one lookup built from two threads at once, where a selector running while the lookup is held reaches, through a cache, what the other thread is still building
/// </summary>
/// <remarks>
/// The first thread's selector runs while the lookup's changes are held, waits until the second thread has begun building over the other grouping and is blocked, then asks for what the second thread is building. This is the interleaving of the hang Grindstone captured opening a workspace on 20 October 2026
/// </remarks>
[TestClass]
public class ConstructionAcrossGroupingsOfOneLookup
{
    static readonly TimeSpan blockingPatience = TimeSpan.FromSeconds(2);
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

    sealed class Rendezvous
    {
        int armed;
        volatile Thread? second;
        volatile bool secondStarted;

        internal readonly ManualResetEventSlim Holding = new();

        internal void Arm() =>
            Volatile.Write(ref armed, 1);

        internal void HoldUntilSecondBlocks()
        {
            if (Interlocked.Exchange(ref armed, 0) != 1)
                return;
            Holding.Set();
            var deadline = DateTime.UtcNow + blockingPatience;
            while (DateTime.UtcNow < deadline && !(secondStarted && second is { } thread && (thread.ThreadState & ThreadState.WaitSleepJoin) != 0))
                Thread.Sleep(1);
        }

        internal void Run(Action first, Action second)
        {
            Exception? firstFault = null;
            Exception? secondFault = null;
            var firstThread = new Thread(() =>
            {
                try
                {
                    first();
                }
                catch (Exception ex)
                {
                    firstFault = ex;
                }
            })
            {
                IsBackground = true
            };
            var secondThread = new Thread(() =>
            {
                if (!Holding.Wait(patience))
                    return;
                secondStarted = true;
                try
                {
                    second();
                }
                catch (Exception ex)
                {
                    secondFault = ex;
                }
            })
            {
                IsBackground = true
            };
            this.second = secondThread;
            secondThread.Start();
            firstThread.Start();
            var firstFinished = firstThread.Join(patience);
            var secondFinished = secondThread.Join(patience);
            Assert.IsTrue(firstFinished && secondFinished, $"building deadlocked (first finished: {firstFinished}, second finished: {secondFinished})");
            Assert.IsTrue(Holding.IsSet, "the first thread's selector never held");
            Assert.IsNull(firstFault, $"the first thread received {firstFault?.GetType().Name}: {firstFault?.Message}");
            Assert.IsNull(secondFault, $"the second thread received {secondFault?.GetType().Name}: {secondFault?.Message}");
        }
    }

    sealed class Probe(IExpressionObserver observer, IObservableGrouping<string, TestPerson> reached, Rendezvous rendezvous)
    {
        internal readonly ConcurrentBag<IDisposable> Built = [];
        internal Expression<Func<IObservableCollectionQuery<char>>>? ReachedExpression;
        internal Expression<Func<TestPerson, IEnumerable<char>>> ReachedSelector = person => person.Name!;
        internal Expression<Func<TestPerson, IEnumerable<char>>>? ReachedSelectorWhileHolding;

        [return: DisposeWhenDiscarded]
        public IObservableCollectionQuery<char> Build() =>
            reached.ObserveSelectMany(ReachedSelector);

        public IEnumerable<char> Reach(TestPerson person)
        {
            var selector = ReachedSelector;
            if (ReachedSelectorWhileHolding is { } selectorWhileHolding && person.Name == "A2")
                selector = selectorWhileHolding;
            rendezvous.HoldUntilSecondBlocks();
            var query = reached.ObserveSelectMany(selector);
            Built.Add(query);
            return query;
        }

        public IEnumerable<char> ReachThroughExpression(TestPerson person)
        {
            rendezvous.HoldUntilSecondBlocks();
            var observableExpression = observer.Observe(ReachedExpression!);
            Built.Add(observableExpression);
            return observableExpression.Evaluation.Result;
        }
    }

    [TestMethod]
    public void ASelectorReachingAQueryUnderConstructionOnAnotherThreadDoesNotDeadlock()
    {
        var source = new ObservableRangeCollection<TestPerson>([new TestPerson("A1"), new TestPerson("B1")]);
        var collectionObserver = CollectionObserverHelpers.Create();
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var lookupQuery = sourceQuery.ObserveToLookup(person => person.Name!.Substring(0, 1));
        var groupings = (IReadOnlyObservableRangeDictionary<string, IObservableGrouping<string, TestPerson>>)lookupQuery;
        var first = groupings["A"];
        var second = groupings["B"];
        var rendezvous = new Rendezvous();
        var probe = new Probe(collectionObserver.ExpressionObserver, second, rendezvous);
        IObservableCollectionQuery<char>? firstQuery = null;
        IObservableCollectionQuery<char>? secondQuery = null;
        rendezvous.Arm();
        rendezvous.Run(() => firstQuery = first.ObserveSelectMany(person => probe.Reach(person)), () => secondQuery = second.ObserveSelectMany(probe.ReachedSelector));
        CollectionAssert.AreEqual("B1".ToList(), secondQuery!.ToList());
        CollectionAssert.AreEqual("B1".ToList(), firstQuery!.ToList());
        firstQuery!.Dispose();
        foreach (var built in probe.Built)
            built.Dispose();
        secondQuery!.Dispose();
        lookupQuery.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ASelectorReachingAQueryUnderConstructionOnAnotherThreadDuringAChangeDoesNotDeadlock()
    {
        var source = new ObservableRangeCollection<TestPerson>([new TestPerson("A1"), new TestPerson("B1")]);
        var collectionObserver = CollectionObserverHelpers.Create();
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var lookupQuery = sourceQuery.ObserveToLookup(person => person.Name!.Substring(0, 1));
        var groupings = (IReadOnlyObservableRangeDictionary<string, IObservableGrouping<string, TestPerson>>)lookupQuery;
        var first = groupings["A"];
        var second = groupings["B"];
        var rendezvous = new Rendezvous();
        var probe = new Probe(collectionObserver.ExpressionObserver, second, rendezvous)
        {
            ReachedSelectorWhileHolding = person => person.Name!.ToLowerInvariant()
        };
        IObservableCollectionQuery<char>? secondQuery = null;
        var firstQuery = first.ObserveSelectMany(person => probe.Reach(person));
        rendezvous.Arm();
        rendezvous.Run(() => source.Add(new TestPerson("A2")), () => secondQuery = second.ObserveSelectMany(probe.ReachedSelectorWhileHolding));
        CollectionAssert.AreEqual("b1".ToList(), secondQuery!.ToList());
        CollectionAssert.AreEqual("B1b1".ToList(), firstQuery.ToList());
        firstQuery.Dispose();
        foreach (var built in probe.Built)
            built.Dispose();
        secondQuery!.Dispose();
        lookupQuery.Dispose();
        sourceQuery.Dispose();
    }

    [TestMethod]
    public void ASelectorReachingAnExpressionUnderConstructionOnAnotherThreadDoesNotDeadlock()
    {
        var source = new ObservableRangeCollection<TestPerson>([new TestPerson("A1"), new TestPerson("B1")]);
        var collectionObserver = CollectionObserverHelpers.Create();
        var sourceQuery = collectionObserver.ObserveReadOnlyList(source);
        var lookupQuery = sourceQuery.ObserveToLookup(person => person.Name!.Substring(0, 1));
        var groupings = (IReadOnlyObservableRangeDictionary<string, IObservableGrouping<string, TestPerson>>)lookupQuery;
        var first = groupings["A"];
        var second = groupings["B"];
        var rendezvous = new Rendezvous();
        var probe = new Probe(collectionObserver.ExpressionObserver, second, rendezvous);
        probe.ReachedExpression = () => probe.Build();
        IObservableCollectionQuery<char>? firstQuery = null;
        IObservableExpression<IObservableCollectionQuery<char>>? secondExpression = null;
        rendezvous.Arm();
        rendezvous.Run(() => firstQuery = first.ObserveSelectMany(person => probe.ReachThroughExpression(person)), () => secondExpression = collectionObserver.ExpressionObserver.Observe(probe.ReachedExpression));
        CollectionAssert.AreEqual("B1".ToList(), secondExpression!.Evaluation.Result.ToList());
        CollectionAssert.AreEqual("B1".ToList(), firstQuery!.ToList());
        firstQuery!.Dispose();
        foreach (var built in probe.Built)
            built.Dispose();
        secondExpression!.Dispose();
        lookupQuery.Dispose();
        sourceQuery.Dispose();
    }
}
