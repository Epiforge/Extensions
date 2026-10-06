namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class Collections
{
    [TestMethod]
    public void ACollectionAlsoObservedForAPropertyIsSubscribedForBoth()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Tags = [] };
        manager.ObservedCollection(() => person.Tags);
        var tags = person.Tags;
        manager.Observed(() => tags.Count);
        Assert.AreEqual(1, tags.CollectionChangedSubscribers);
        tags.Add("pioneer");
        Assert.AreEqual(2, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void ACollectionNoLongerObservedAsACollectionIsReleased()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Tags = [] };
        manager.ObservedCollection(() => person.Tags);
        manager.NotifyCycleEnded();
        manager.Observed(() => person.Tags);
        manager.NotifyCycleEnded();
        Assert.AreEqual(0, person.Tags.CollectionChangedSubscribers);
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        person.Tags.Add("pioneer");
        Assert.AreEqual(0, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AConstantCollectionIsObserved()
    {
        var (manager, component) = Managers.CreateInitialized();
        var tags = new TrackedCollection<string>();
        Assert.AreSame(tags, manager.ObservedCollection(() => tags));
        Assert.AreEqual(1, tags.CollectionChangedSubscribers);
        tags.Add("pioneer");
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AReplacedCollectionIsReleased()
    {
        var (manager, component) = Managers.CreateInitialized();
        var first = new TrackedCollection<string>();
        var second = new TrackedCollection<string>();
        var person = new Person { Tags = first };
        manager.ObservedCollection(() => person.Tags);
        manager.NotifyCycleEnded();
        person.Tags = second;
        Assert.AreSame(second, manager.ObservedCollection(() => person.Tags));
        manager.NotifyCycleEnded();
        Assert.AreEqual(0, first.CollectionChangedSubscribers);
        Assert.AreEqual(1, second.CollectionChangedSubscribers);
        var before = component.StateHasChangedCalls;
        first.Add("ignored");
        Assert.AreEqual(before, component.StateHasChangedCalls);
        second.Add("noticed");
        Assert.AreEqual(before + 1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void ObservedDoesNotSubscribeToTheCollection()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Tags = [] };
        manager.Observed(() => person.Tags);
        Assert.AreEqual(0, person.Tags.CollectionChangedSubscribers);
    }

    [TestMethod]
    public void ObservedCollectionSubscribesToTheCollection()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Tags = [] };
        Assert.AreSame(person.Tags, manager.ObservedCollection(() => person.Tags));
        Assert.AreEqual(1, person.Tags.CollectionChangedSubscribers);
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        person.Tags.Add("pioneer");
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }
}
