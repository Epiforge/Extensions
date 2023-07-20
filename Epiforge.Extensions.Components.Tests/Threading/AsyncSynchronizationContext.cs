namespace Epiforge.Extensions.Components.Tests.Threading;

[TestClass]
public class AsyncSynchronizationContext
{
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
}
