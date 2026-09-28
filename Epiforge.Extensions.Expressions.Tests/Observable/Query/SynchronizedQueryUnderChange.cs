namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A query synchronized with a context, a lock or a callback, whose source changes on another thread while it is delivering a change, and which is changed or built over on a second thread at the same time
/// </summary>
/// <remarks>
/// Each row holds one thread, the context's own or one holding the lock, until a change made on another thread has reached the point where it must wait on the first, then lets the first thread change the same source or build a query over the synchronized one. Every change and build runs on a thread of its own, so none waits on a busy thread pool. A correct query applies every change and ends agreeing with its source, and a query built over it ends agreeing with it; the test cannot tell a thread which will never finish from one which is merely slow, so it gives each ten seconds and reports them as stuck when they have not finished by then. What is stuck is left behind rather than disposed of, since disposing of it would wait on it too
/// </remarks>
[TestClass]
public class SynchronizedQueryUnderChange
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A context which runs what is sent or posted to it, in order, on one thread of its own, counts what it has been given to run, and signals the first time something is sent to it from another thread
    /// </summary>
    public sealed class PumpedSynchronizationContext :
        SynchronizationContext
    {
        readonly BlockingCollection<(SendOrPostCallback callback, object? state, ManualResetEventSlim? done)> queue = [];

        public PumpedSynchronizationContext() =>
            new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach (var (callback, state, done) in queue.GetConsumingEnumerable())
                {
                    try
                    {
                        callback(state);
                    }
                    catch (Exception ex)
                    {
                        Fault ??= ex;
                    }
                    finally
                    {
                        done?.Set();
                    }
                }
            })
            { IsBackground = true }.Start();

        int queued;

        public Exception? Fault { get; private set; }

        public int Queued =>
            Volatile.Read(ref queued);

        public ManualResetEventSlim SendRequested { get; } = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            queue.Add((d, state, null));
            Interlocked.Increment(ref queued);
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (Current == this)
            {
                d(state);
                return;
            }
            using var done = new ManualResetEventSlim();
            queue.Add((d, state, done));
            Interlocked.Increment(ref queued);
            SendRequested.Set();
            done.Wait();
        }
    }

    /// <summary>
    /// A list which is safe to read while another thread changes it and announces each change after making it, as the readme asks of anything observed across threads
    /// </summary>
    public sealed class GuardedList<T>(IEnumerable<T> items) :
        IReadOnlyList<T>,
        INotifyCollectionChanged
    {
        readonly object access = new();
        readonly List<T> items = [.. items];

        public T this[int index]
        {
            get
            {
                lock (access)
                    return items[index];
            }
        }

        public int Count
        {
            get
            {
                lock (access)
                    return items.Count;
            }
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public void Add(T item)
        {
            int index;
            lock (access)
            {
                index = items.Count;
                items.Add(item);
            }
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (access)
                return items.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    static bool Race(PumpedSynchronizationContext context, Action changeElsewhere, Action changeOnContext)
    {
        var contextHeld = new ManualResetEventSlim();
        var contextReleased = new ManualResetEventSlim();
        var contextChanged = new ManualResetEventSlim();
        Exception? contextFault = null;
        context.Post(_ =>
        {
            contextHeld.Set();
            if (!contextReleased.Wait(patience))
                return;
            try
            {
                changeOnContext();
            }
            catch (Exception ex)
            {
                contextFault = ex;
            }
            contextChanged.Set();
        }, null);
        if (!contextHeld.Wait(patience))
            Assert.Fail("the context never ran what was posted to it");
        var changing = Task.Factory.StartNew(changeElsewhere, TaskCreationOptions.LongRunning);
        if (!context.SendRequested.Wait(patience))
            Assert.Fail("the change made elsewhere never asked the context to apply it");
        contextReleased.Set();
        var finished = changing.Wait(patience) & contextChanged.Wait(patience);
        if (!finished)
            return false;
        Assert.IsNull(contextFault, $"the change on the context threw {contextFault?.GetType().Name}: {contextFault?.Message}");
        Assert.IsNull(context.Fault, $"the context threw {context.Fault?.GetType().Name}: {context.Fault?.Message}");
        return true;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ACollectionSynchronizedWithAContextAppliesAChangeMadeOnTheContextWhileDeliveringOneMadeElsewhere(bool throughAFilter)
    {
        var context = new PumpedSynchronizationContext();
        var list = new GuardedList<int>([1]);
        var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(list);
        var synchronizedOver = throughAFilter ? observed.ObserveWhere(number => number > 0) : observed;
        var synchronized = synchronizedOver.ObserveUsingSynchronizationContext(context);
        if (!Race(context, () => list.Add(2), () => list.Add(3)))
            Assert.Fail("neither change finished: each waits on the other");
        var expected = list.OrderBy(number => number).ToList();
        var actual = synchronized.OrderBy(number => number).ToList();
        synchronized.Dispose();
        if (throughAFilter)
            synchronizedOver.Dispose();
        observed.Dispose();
        CollectionAssert.AreEqual(expected, actual, $"the synchronized query ends holding {string.Join(",", actual)}");
    }

    [TestMethod]
    public void ADictionarySynchronizedWithAContextAppliesAChangeMadeOnTheContextWhileDeliveringOneMadeElsewhere()
    {
        var context = new PumpedSynchronizationContext();
        var dictionary = new ObservableConcurrentDictionary<string, int>();
        dictionary.TryAdd("a", 1);
        var observed = CollectionObserverHelpers.Create().ObserveReadOnlyDictionary(dictionary);
        var synchronized = observed.ObserveUsingSynchronizationContext(context);
        if (!Race(context, () => dictionary.TryAdd("b", 2), () => dictionary.TryAdd("c", 3)))
            Assert.Fail("neither change finished: each waits on the other");
        var expected = dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}").ToList();
        var actual = synchronized.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}").ToList();
        synchronized.Dispose();
        observed.Dispose();
        CollectionAssert.AreEqual(expected, actual, $"the synchronized query ends holding {string.Join(",", actual)}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ACollectionSynchronizedWithAContextCanBeBuiltOverOnAnotherThreadWhileAChangeWaitsForTheContext(bool eventually)
    {
        var context = new PumpedSynchronizationContext();
        var list = new ObservableRangeCollection<int>([1]);
        var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(list);
        var synchronized = eventually ? observed.ObserveUsingSynchronizationContextEventually(context) : observed.ObserveUsingSynchronizationContext(context);
        var contextHeld = new ManualResetEventSlim();
        var contextReleased = new ManualResetEventSlim();
        context.Post(_ =>
        {
            contextHeld.Set();
            contextReleased.Wait(patience);
        }, null);
        if (!contextHeld.Wait(patience))
            Assert.Fail("the context never ran what was posted to it");
        var changing = Task.Factory.StartNew(() => list.Add(2), TaskCreationOptions.LongRunning);
        if (!SpinWait.SpinUntil(() => context.Queued >= 2, patience))
            Assert.Fail("the change never gave the context anything to apply");
        var building = Task.Factory.StartNew(() => synchronized.ObserveWhere(number => number > 0), TaskCreationOptions.LongRunning);
        SpinWait.SpinUntil(() => building.IsCompleted || context.Queued >= 3, patience);
        contextReleased.Set();
        if (!(changing.Wait(patience) & building.Wait(patience)))
            Assert.Fail("the change and the build never finished: each waits on the other");
        var built = building.Result;
        var expected = list.ToList();
        var actual = synchronized.ToList();
        var builtActual = context.Send(() => built.ToList());
        built.Dispose();
        synchronized.Dispose();
        observed.Dispose();
        CollectionAssert.AreEqual(expected, actual, $"the synchronized query ends holding {string.Join(",", actual)}");
        CollectionAssert.AreEqual(expected, builtActual, $"the query built over it ends holding {string.Join(",", builtActual)}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ADictionarySynchronizedWithAContextCanBeBuiltOverOnAnotherThreadWhileAChangeWaitsForTheContext(bool eventually)
    {
        var context = new PumpedSynchronizationContext();
        var dictionary = new ObservableDictionary<string, int> { ["a"] = 1 };
        var observed = CollectionObserverHelpers.Create().ObserveReadOnlyDictionary(dictionary);
        var synchronized = eventually ? observed.ObserveUsingSynchronizationContextEventually(context) : observed.ObserveUsingSynchronizationContext(context);
        var contextHeld = new ManualResetEventSlim();
        var contextReleased = new ManualResetEventSlim();
        context.Post(_ =>
        {
            contextHeld.Set();
            contextReleased.Wait(patience);
        }, null);
        if (!contextHeld.Wait(patience))
            Assert.Fail("the context never ran what was posted to it");
        var changing = Task.Factory.StartNew(() => dictionary.Add("b", 2), TaskCreationOptions.LongRunning);
        if (!SpinWait.SpinUntil(() => context.Queued >= 2, patience))
            Assert.Fail("the change never gave the context anything to apply");
        var building = Task.Factory.StartNew(() => synchronized.ObserveWhere((key, value) => value > 0), TaskCreationOptions.LongRunning);
        SpinWait.SpinUntil(() => building.IsCompleted || context.Queued >= 3, patience);
        contextReleased.Set();
        if (!(changing.Wait(patience) & building.Wait(patience)))
            Assert.Fail("the change and the build never finished: each waits on the other");
        var built = building.Result;
        var expected = dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}").ToList();
        var actual = synchronized.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}").ToList();
        var builtActual = context.Send(() => built.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}").ToList());
        built.Dispose();
        synchronized.Dispose();
        observed.Dispose();
        CollectionAssert.AreEqual(expected, actual, $"the synchronized query ends holding {string.Join(",", actual)}");
        CollectionAssert.AreEqual(expected, builtActual, $"the query built over it ends holding {string.Join(",", builtActual)}");
    }

    [TestMethod]
    public void ACollectionSynchronizedWithALockAppliesAChangeMadeUnderTheLockWhileDeliveringOneMadeElsewhere()
    {
        var syncRoot = new object();
        var list = new GuardedList<int>([1]);
        var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(list);
        var deliveryBegan = new ManualResetEventSlim();
        var armed = 1;
        observed.CollectionChanged += (sender, e) =>
        {
            if (Interlocked.Exchange(ref armed, 0) == 1)
                deliveryBegan.Set();
        };
        var synchronized = observed.ObserveUsingSyncRoot(syncRoot);
        var lockHeld = new ManualResetEventSlim();
        var lockReleased = new ManualResetEventSlim();
        var changingUnderTheLock = Task.Factory.StartNew(() =>
        {
            lock (syncRoot)
            {
                lockHeld.Set();
                if (lockReleased.Wait(patience))
                    list.Add(3);
            }
        }, TaskCreationOptions.LongRunning);
        if (!lockHeld.Wait(patience))
            Assert.Fail("the lock was never taken");
        var changing = Task.Factory.StartNew(() => list.Add(2), TaskCreationOptions.LongRunning);
        if (!deliveryBegan.Wait(patience))
            Assert.Fail("the change made elsewhere was never delivered");
        lockReleased.Set();
        if (!(changing.Wait(patience) & changingUnderTheLock.Wait(patience)))
            Assert.Fail("neither change finished: each waits on the other");
        var expected = list.OrderBy(number => number).ToList();
        List<int> actual;
        lock (syncRoot)
            actual = synchronized.OrderBy(number => number).ToList();
        synchronized.Dispose();
        observed.Dispose();
        CollectionAssert.AreEqual(expected, actual, $"the synchronized query ends holding {string.Join(",", actual)}");
    }

    [TestMethod]
    public void ACollectionSynchronizedWithACallbackWhichSendsToAContextAppliesAChangeMadeOnTheContextWhileDeliveringOneMadeElsewhere()
    {
        var context = new PumpedSynchronizationContext();
        var list = new GuardedList<int>([1]);
        var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(list);
        var synchronized = observed.ObserveUsingSynchronizationCallback(new object(), (collection, state, accessMethod, writeAccess) => context.Send(_ => accessMethod(), null));
        if (!Race(context, () => list.Add(2), () => list.Add(3)))
            Assert.Fail("neither change finished: each waits on the other");
        var expected = list.OrderBy(number => number).ToList();
        var actual = context.Send(() => synchronized.OrderBy(number => number).ToList());
        synchronized.Dispose();
        observed.Dispose();
        CollectionAssert.AreEqual(expected, actual, $"the synchronized query ends holding {string.Join(",", actual)}");
    }
}
