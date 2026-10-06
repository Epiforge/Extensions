namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class ManagerState
{
    [TestMethod]
    public void DisposingReleasesEverySubscription()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Name = "Grace" }, Tags = [] };
        manager.Observed(() => person.Partner.Name);
        manager.ObservedCollection(() => person.Tags);
        manager.Dispose();
        Assert.AreEqual(0, person.PropertyChangedSubscribers);
        Assert.AreEqual(0, person.Partner.PropertyChangedSubscribers);
        Assert.AreEqual(0, person.Tags.CollectionChangedSubscribers);
    }

    [TestMethod]
    public void DisposingTwiceIsHarmless()
    {
        var (manager, _) = Managers.CreateInitialized();
        manager.Dispose();
        manager.Dispose();
    }

    [TestMethod]
    public void EachResolutionIsANewManager() =>
        Assert.AreNotSame(Managers.Create(), Managers.Create());

    [TestMethod]
    public void InitializingTwiceIsRefused()
    {
        var (manager, _) = Managers.CreateInitialized();
        Assert.ThrowsException<InvalidOperationException>(() => manager.Initialize(new RecordingComponent()));
    }

    [TestMethod]
    public void NothingWorksAfterDisposal()
    {
        var manager = Managers.Create();
        manager.Dispose();
        var person = new Person { Name = "Ada", Tags = [] };
        Assert.ThrowsException<ObjectDisposedException>(() => manager.Initialize(new RecordingComponent()));
        Assert.ThrowsException<ObjectDisposedException>(() => manager.Observed(() => person.Name));
        Assert.ThrowsException<ObjectDisposedException>(() => manager.ObservedCollection(() => person.Tags));
        Assert.ThrowsException<ObjectDisposedException>(() => manager.Binding(() => person.Name));
        Assert.ThrowsException<ObjectDisposedException>(() => manager.Binding(() => person.Name, name => name, name => name));
        Assert.ThrowsException<ObjectDisposedException>(manager.NotifyCycleEnded);
    }

    [TestMethod]
    public void NothingWorksBeforeInitialization()
    {
        var manager = Managers.Create();
        var person = new Person { Name = "Ada", Tags = [] };
        Assert.ThrowsException<InvalidOperationException>(() => manager.Observed(() => person.Name));
        Assert.ThrowsException<InvalidOperationException>(() => manager.ObservedCollection(() => person.Tags));
        Assert.ThrowsException<InvalidOperationException>(() => manager.Binding(() => person.Name));
        Assert.ThrowsException<InvalidOperationException>(() => manager.Binding(() => person.Name, name => name, name => name));
        Assert.ThrowsException<InvalidOperationException>(manager.NotifyCycleEnded);
    }
}
