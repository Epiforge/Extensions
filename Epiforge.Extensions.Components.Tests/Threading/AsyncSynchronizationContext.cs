namespace Epiforge.Extensions.Components.Tests.Threading;

[TestClass]
public class AsyncSynchronizationContext
{
    [TestMethod]
    public void CreateCopyReturnsTheSameContext()
    {
        using var synchronizationContext = new Components.Threading.AsyncSynchronizationContext();
        Assert.AreSame(synchronizationContext, synchronizationContext.CreateCopy());
    }

    [TestMethod]
    [Timeout(5000)]
    public void CurrentIsTheContextWithinACallback()
    {
        SynchronizationContext? observed = null;
        using var synchronizationContext = new Components.Threading.AsyncSynchronizationContext();
        synchronizationContext.Send(_ => observed = SynchronizationContext.Current, null);
        Assert.AreSame(synchronizationContext, observed);
    }

    [TestMethod]
    public async Task PostAsync()
    {
        var observableCollection = new ObservableCollection<int>();
        var manualResetEvent = new AsyncManualResetEvent(false);
        void collectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            manualResetEvent!.Set();
            manualResetEvent.Reset();
        }
        observableCollection.CollectionChanged += collectionChanged;
        var queryChangedTask = manualResetEvent.WaitAsync();
        using (var synchronizationContext = new Components.Threading.AsyncSynchronizationContext())
        {
            synchronizationContext.Post(_ => observableCollection.Add(1), null);
            await queryChangedTask;
        }
        Assert.AreEqual(1, observableCollection.Count);
        observableCollection.CollectionChanged -= collectionChanged;
    }

    [TestMethod]
    public void Send()
    {
        var list = new List<int>();
        using (var synchronizationContext = new Components.Threading.AsyncSynchronizationContext())
        {
            synchronizationContext.Send(_ => list.Add(1), null);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => synchronizationContext.Send(_ => list[1].ToString(), null));
        }
        Assert.AreEqual(1, list.Count);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task SendFromWithinAPostedCallbackDoesNotDeadlockAsync()
    {
        var completed = new AsyncManualResetEvent(false);
        var value = 0;
        using (var synchronizationContext = new Components.Threading.AsyncSynchronizationContext())
        {
            var completedTask = completed.WaitAsync();
            synchronizationContext.Post(_ =>
            {
                value = synchronizationContext.Send(() => 42);
                completed.Set();
            }, null);
            await completedTask;
        }
        Assert.AreEqual(42, value);
    }

    [TestMethod]
    [Timeout(5000)]
    public void SendFromWithinTheContextRunsBeforeCallbacksQueuedByIt()
    {
        var order = new List<string>();
        using var completed = new ManualResetEventSlim(false);
        using (var synchronizationContext = new Components.Threading.AsyncSynchronizationContext())
        {
            synchronizationContext.Post(_ =>
            {
                synchronizationContext.Post(state =>
                {
                    order.Add("posted from within");
                    completed.Set();
                }, null);
                synchronizationContext.Send(state => order.Add("sent from within"), null);
                order.Add("after the send");
            }, null);
            completed.Wait();
        }
        Assert.AreEqual(3, order.Count);
        Assert.AreEqual("sent from within", order[0]);
        Assert.AreEqual("after the send", order[1]);
        Assert.AreEqual("posted from within", order[2]);
    }

    [TestMethod]
    [Timeout(5000)]
    public void SendFromWithinTheContextDoesNotDeadlock()
    {
        var list = new List<int>();
        using (var synchronizationContext = new Components.Threading.AsyncSynchronizationContext())
            synchronizationContext.Send(_ =>
            {
                synchronizationContext.Send(state => list.Add(1), null);
                list.Add(2);
            }, null);
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(1, list[0]);
        Assert.AreEqual(2, list[1]);
    }
}
