namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// Changes raised on one thread while another is building or evaluating an observation of what changed
/// </summary>
/// <remarks>
/// Each source here holds a read part way through, on a thread of the test's choosing, until the test has made its change from another thread, so every row forces the interleaving it names rather than waiting for a scheduler to produce it. A row which fails therefore fails every time, and one which passes has survived the interleaving rather than missed it
/// </remarks>
[TestClass]
public class ConcurrentChange
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A value whose next read, once armed, signals that it has read and then waits to be released before returning what it read
    /// </summary>
    public sealed class Gated :
        INotifyPropertyChanged
    {
        public Gated(int value) =>
            this.value = value;

        int armed;
        int value;

        public ManualResetEventSlim Reading { get; } = new();

        public ManualResetEventSlim Released { get; } = new();

        public int Value
        {
            get
            {
                var read = Volatile.Read(ref value);
                if (Interlocked.Exchange(ref armed, 0) == 1)
                {
                    Reading.Set();
                    if (!Released.Wait(patience))
                        throw new TimeoutException("the test never released a held read");
                }
                return read;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Arm() =>
            Volatile.Write(ref armed, 1);

        public void Change(int value)
        {
            Volatile.Write(ref this.value, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    /// <summary>
    /// Combines two values, and once armed signals that it has been called and waits to be released before returning what it combined
    /// </summary>
    public sealed class Combiner
    {
        int armed;

        public ManualResetEventSlim Combining { get; } = new();

        public ManualResetEventSlim Released { get; } = new();

        public void Arm() =>
            Volatile.Write(ref armed, 1);

        public int Combine(int tens, int ones)
        {
            var combined = tens * 10 + ones;
            if (Interlocked.Exchange(ref armed, 0) == 1)
            {
                Combining.Set();
                if (!Released.Wait(patience))
                    throw new TimeoutException("the test never released a held combination");
            }
            return combined;
        }
    }

    /// <summary>
    /// Makes something the observer disposes of, and once armed signals that it has made it and waits to be released before returning it
    /// </summary>
    public sealed class Maker
    {
        int armed;

        public ConcurrentQueue<Made> Made { get; } = new();

        public ManualResetEventSlim Making { get; } = new();

        public ManualResetEventSlim Released { get; } = new();

        public void Arm() =>
            Volatile.Write(ref armed, 1);

        public Made Make(int value)
        {
            var made = new Made(value);
            Made.Enqueue(made);
            if (Interlocked.Exchange(ref armed, 0) == 1)
            {
                Making.Set();
                if (!Released.Wait(patience))
                    throw new TimeoutException("the test never released a held making");
            }
            return made;
        }
    }

    public sealed class Made(int value) :
        IDisposable
    {
        int disposals;

        public int Disposals =>
            Volatile.Read(ref disposals);

        public int Value { get; } = value;

        public void Dispose() =>
            Interlocked.Increment(ref disposals);
    }

    public sealed class Pair(Gated tens, Gated ones, Combiner combiner)
    {
        public Combiner Combiner { get; } = combiner;

        public Gated Ones { get; } = ones;

        public Gated Tens { get; } = tens;
    }

    sealed class RecordingLogger :
        Microsoft.Extensions.Logging.ILogger
    {
        readonly ConcurrentQueue<(Microsoft.Extensions.Logging.EventId EventId, Exception? Exception)> records = new();

        public IEnumerable<Exception> Faults =>
            records.Where(record => record.EventId == EventIds.Epiforge_Extensions_Expressions_ExpressionFaulted).Select(record => record.Exception!);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) =>
            true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            records.Enqueue((eventId, exception));
    }

    static void AssertMechanism(ExpressionObserver observer, bool useDirectSubscription)
    {
        if (useDirectSubscription)
            Assert.AreEqual(0, observer.CachedObservableExpressions, "the fast path declined the shape this test exists to cover");
    }

    static void AwaitSignal(ManualResetEventSlim signal, string what)
    {
        if (!signal.Wait(patience))
            Assert.Fail($"the {what} never began");
    }

    static T Finish<T>(Task<T> task)
    {
        if (!task.Wait(patience))
            Assert.Fail("the observation never finished building");
        return task.Result;
    }

    /// <summary>
    /// A change made while the observation's first read of it is under way reaches the observation, rather than the read finishing afterward and leaving the value it began with
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AChangeDuringConstructionIsNotLost(bool useDirectSubscription)
    {
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var gated = new Gated(1);
        gated.Arm();
        var building = Task.Run(() => observer.Observe(g => g.Value, gated));
        AwaitSignal(gated.Reading, "read");
        gated.Change(2);
        gated.Released.Set();
        using var observation = Finish(building);
        AssertMechanism(observer, useDirectSubscription);
        Assert.AreEqual(2, observation.Evaluation.Result, "the change made during construction was lost");
    }

    /// <summary>
    /// A change to one operand made while a change to the other is being combined reaches the observation, rather than being turned away because an evaluation was already under way on another thread
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AChangeDuringAnotherChangesEvaluationIsNotLost(bool useDirectSubscription)
    {
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        var pair = new Pair(new Gated(1), new Gated(1), new Combiner());
        using var observation = observer.Observe(p => p.Combiner.Combine(p.Tens.Value, p.Ones.Value), pair);
        AssertMechanism(observer, useDirectSubscription);
        Assert.AreEqual(11, observation.Evaluation.Result);
        pair.Combiner.Arm();
        var changing = Task.Run(() => pair.Tens.Change(2));
        AwaitSignal(pair.Combiner.Combining, "combination");
        pair.Ones.Change(2);
        pair.Combiner.Released.Set();
        if (!changing.Wait(patience))
            Assert.Fail("the first change never finished");
        Assert.AreEqual(22, observation.Evaluation.Result, "the change made while another was being combined was lost");
    }

    /// <summary>
    /// Waiting for a condition which a change on another thread makes true while the wait is being set up completes, which is the shape in which a consumer met this
    /// </summary>
    [TestMethod]
    public void AConditionMadeTrueDuringConstructionIsMet()
    {
        var observer = ExpressionObserverHelpers.Create();
        var gated = new Gated(1);
        gated.Arm();
        var waiting = Task.Run(() => observer.ConditionAsync(() => gated.Value == 2));
        AwaitSignal(gated.Reading, "read");
        gated.Change(2);
        gated.Released.Set();
        if (!waiting.Wait(patience))
            Assert.Fail("the condition was made true during construction and never met");
    }

    /// <summary>
    /// A binary operation whose left operand announces a change while its right operand is still being built is not evaluated until it has both
    /// </summary>
    [TestMethod]
    public void ABinaryOperationIsNotEvaluatedBeforeItIsBuilt()
    {
        var logger = new RecordingLogger();
        var observer = ExpressionObserverHelpers.Create(false, new ExpressionObserverOptions { Logger = logger });
        var tens = new Gated(1);
        var ones = new Gated(1);
        ones.Arm();
        var building = Task.Run(() => observer.Observe(() => tens.Value + ones.Value));
        AwaitSignal(ones.Reading, "read");
        tens.Change(2);
        ones.Released.Set();
        using var observation = Finish(building);
        Assert.AreEqual(3, observation.Evaluation.Result);
        Assert.AreEqual(0, logger.Faults.Count(), $"a node faulted while being built: {string.Join("; ", logger.Faults.Select(fault => fault.GetType().Name))}");
    }

    /// <summary>
    /// An indexer whose collection announces a reset while its index is still being built is not evaluated until it has its index
    /// </summary>
    [TestMethod]
    public void AnIndexerIsNotEvaluatedBeforeItIsBuilt()
    {
        var logger = new RecordingLogger();
        var observer = ExpressionObserverHelpers.Create(false, new ExpressionObserverOptions { Logger = logger });
        var collection = new ObservableRangeCollection<string>(["b", "a"]);
        var index = new Gated(0);
        index.Arm();
        var building = Task.Run(() => observer.Observe(() => collection[index.Value]));
        AwaitSignal(index.Reading, "read");
        collection.Reset(["a", "b"]);
        index.Released.Set();
        using var observation = Finish(building);
        Assert.AreEqual("a", observation.Evaluation.Result);
        Assert.AreEqual(0, logger.Faults.Count(), $"a node faulted while being built: {string.Join("; ", logger.Faults.Select(fault => fault.GetType().Name))}");
    }

    /// <summary>
    /// An observation disposed of while a change is being evaluated on another thread disposes of everything that evaluation made, and of nothing twice
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DisposalDuringAnEvaluationDisposesOfWhatItMadeOnce(bool useDirectSubscription)
    {
        var maker = new Maker();
        var options = new ExpressionObserverOptions { PreferAsyncDisposal = false };
        options.AddMethodReturnValueDisposal(typeof(Maker).GetMethod(nameof(Maker.Make))!);
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, options);
        var gated = new Gated(1);
        var observation = observer.Observe(g => maker.Make(g.Value), gated);
        maker.Arm();
        var changing = Task.Run(() => gated.Change(2));
        AwaitSignal(maker.Making, "making");
        var disposing = Task.Run(observation.Dispose);
        disposing.Wait(TimeSpan.FromMilliseconds(200));
        maker.Released.Set();
        if (!changing.Wait(patience) || !disposing.Wait(patience))
            Assert.Fail("the change or the disposal never finished");
        foreach (var made in maker.Made)
            Assert.AreEqual(1, made.Disposals, $"what was made for {made.Value} was disposed of {made.Disposals} times");
        Assert.AreEqual(0, observer.CachedObservableExpressions, "the disposed observation left nodes cached");
    }

    /// <summary>
    /// Observations sharing nodes, built and disposed of on many threads at once, leave nothing cached once all of them are disposed of
    /// </summary>
    /// <remarks>
    /// This row is the one which cannot force its interleaving: a release is lost only when two meet inside a disposal, and nothing public can hold one there. It runs enough of them that on 6.1.0 the loss was seen in every run
    /// </remarks>
    [TestMethod]
    public void ConcurrentReleasesOfSharedNodesAreNotLost()
    {
        var observer = ExpressionObserverHelpers.Create(false);
        Parallel.For(0, 64000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var source = new Gated(i);
            observer.Observe(() => source.Value == 3).Dispose();
        });
        Assert.AreEqual(0, observer.CachedObservableExpressions, "releases made at once by different observations of a shared node were lost");
    }
}
