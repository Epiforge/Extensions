namespace Epiforge.Extensions.Expressions.Tests.Observable;

/// <summary>
/// An observer told to wait for asynchronous disposals, disposing of a value on the thread of a context or a scheduler to which that value's disposal returns, and on a thread with neither
/// </summary>
/// <remarks>
/// Most rows run an observation on a context with one thread of its own, or in a task on a scheduler which runs one task at a time, and the value it disposes of finishes its disposal on whichever context or scheduler it began on, as an awaiting method does by default. A correct observer waits for the disposal without waiting for that thread or scheduler, which is the one waiting; the test cannot tell a wait which will never end from one which is merely slow, so it gives the row ten seconds and reports it as stuck when it has not finished by then. The last row runs on a thread with neither, where the disposal returns to the thread pool and the observer can wait for it where it is
/// </remarks>
[TestClass]
public class AsyncDisposalOnAContext
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A value whose disposal yields to the context it began on before it finishes
    /// </summary>
    public sealed class ContextBoundDisposable(int number) :
        IAsyncDisposable
    {
        int disposals;

        public int Disposals =>
            Volatile.Read(ref disposals);

        public int Number { get; } = number;

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Interlocked.Increment(ref disposals);
        }
    }

    public sealed class Holder :
        PropertyChangeNotifier
    {
        int value;

        public int Value
        {
            get => value;
            set => SetBackedProperty(ref this.value, in value);
        }
    }

    static ExpressionObserver CreateObserver()
    {
        var options = new ExpressionObserverOptions { BlockOnAsyncDisposal = true };
        options.AddConstructedTypeDisposal(typeof(ContextBoundDisposable), typeof(int));
        return new ExpressionObserver(options);
    }

    static void RunOnContext(Action action)
    {
        var context = new Query.SynchronizedQueryUnderChange.PumpedSynchronizationContext();
        var finished = new ManualResetEventSlim();
        context.Post(_ =>
        {
            action();
            finished.Set();
        }, null);
        if (!finished.Wait(patience))
            Assert.Fail($"the context's thread never finished: it waits on a disposal which waits on it{(context.Fault is { } fault ? $", or threw {fault.GetType().Name}: {fault.Message}" : string.Empty)}");
        Assert.IsNull(context.Fault, $"the context's thread threw {context.Fault?.GetType().Name}: {context.Fault?.Message}");
    }

    [TestMethod]
    public void AValueReplacedInATaskOnAnExclusiveSchedulerIsDisposedOfWithoutWaitingForThatScheduler()
    {
        var observer = CreateObserver();
        var holder = new Holder { Value = 1 };
        ContextBoundDisposable? replaced = null;
        var scheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;
        var task = Task.Factory.StartNew(() =>
        {
            using var observation = observer.Observe(() => new ContextBoundDisposable(holder.Value));
            replaced = observation.Evaluation.Result;
            holder.Value = 2;
        }, CancellationToken.None, TaskCreationOptions.None, scheduler);
        if (!task.Wait(patience))
            Assert.Fail("the scheduler's task never finished: it waits on a disposal which waits on it");
        Assert.AreEqual(1, replaced!.Disposals, "the replaced value was not disposed of exactly once");
    }

    [TestMethod]
    public void AValueReplacedOnTheContextsThreadIsDisposedOfWithoutWaitingForThatThread()
    {
        var observer = CreateObserver();
        var holder = new Holder { Value = 1 };
        ContextBoundDisposable? replaced = null;
        RunOnContext(() =>
        {
            using var observation = observer.Observe(() => new ContextBoundDisposable(holder.Value));
            replaced = observation.Evaluation.Result;
            holder.Value = 2;
        });
        Assert.AreEqual(1, replaced!.Disposals, "the replaced value was not disposed of exactly once");
    }

    [TestMethod]
    public void AHeldValueIsDisposedOfWithTheObservationOnTheContextsThreadWithoutWaitingForThatThread()
    {
        var observer = CreateObserver();
        RunOnContext(() =>
        {
            using var observation = observer.Observe(() => new ContextBoundDisposable(1).Number);
        });
    }

    [TestMethod]
    public void AValueReplacedWhereTheDisposalHasNothingToReturnToIsDisposedOfBeforeTheChangeReturns()
    {
        var observer = CreateObserver();
        var holder = new Holder { Value = 1 };
        var task = Task.Factory.StartNew(() =>
        {
            using var observation = observer.Observe(() => new ContextBoundDisposable(holder.Value));
            var replaced = observation.Evaluation.Result;
            holder.Value = 2;
            return replaced.Disposals;
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        if (!task.Wait(patience))
            Assert.Fail("the change never finished");
        Assert.AreEqual(1, task.Result, "the replaced value was not disposed of by the time the change returned");
    }
}
