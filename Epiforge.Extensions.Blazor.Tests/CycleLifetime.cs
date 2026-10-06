namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class CycleLifetime
{
    [TestMethod]
    public void AnObjectNoLongerReachedIsReleased()
    {
        var (manager, component) = Managers.CreateInitialized();
        var first = new Person { Name = "Grace" };
        var second = new Person { Name = "Hedy" };
        var person = new Person { Partner = first };
        manager.Observed(() => person.Partner.Name);
        manager.NotifyCycleEnded();
        person.Partner = second;
        Assert.AreEqual("Hedy", manager.Observed(() => person.Partner.Name));
        manager.NotifyCycleEnded();
        Assert.AreEqual(0, first.PropertyChangedSubscribers);
        Assert.AreEqual(1, second.PropertyChangedSubscribers);
        var before = component.StateHasChangedCalls;
        first.Name = "Ignored";
        Assert.AreEqual(before, component.StateHasChangedCalls);
        second.Name = "Noticed";
        Assert.AreEqual(before + 1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AnObjectReleasedAfterAFailedReadIsSubscribedAgainWhenReachedAgain()
    {
        var (manager, component) = Managers.CreateInitialized();
        var third = new Person { Name = "Hedy" };
        var second = new Person { Partner = third };
        var person = new Person { Partner = second };
        manager.Observed(() => person.Partner.Partner.Name);
        manager.NotifyCycleEnded();
        second.Partner = null;
        Assert.ThrowsException<ArgumentException>(() => manager.Observed(() => person.Partner.Partner!.Name));
        manager.NotifyCycleEnded();
        Assert.AreEqual(0, third.PropertyChangedSubscribers);
        second.Partner = third;
        Assert.AreEqual("Hedy", manager.Observed(() => person.Partner.Partner.Name));
        Assert.AreEqual(1, third.PropertyChangedSubscribers);
        var before = component.StateHasChangedCalls;
        third.Name = "Grace";
        Assert.AreEqual(before + 1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void BindingsConfiguredAtTheEndOfACycleSurviveIt()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        component.ConfiguringBindings = () => manager.Binding(() => person.Name);
        manager.NotifyCycleEnded();
        manager.NotifyCycleEnded();
        Assert.AreEqual(2, component.ConfigureBindingsCalls);
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
    }

    [TestMethod]
    public void ManyRootsAreObservedAndReleasedLikeAFew()
    {
        var (manager, component) = Managers.CreateInitialized();
        var people = Enumerable.Range(0, 40).Select(i => new Person { Name = $"Person {i}", Partner = new Person { Name = $"Partner {i}" } }).ToList();
        foreach (var person in people)
        {
            Assert.AreEqual(person.Name, manager.Observed(() => person.Name));
            Assert.AreEqual(person.Partner!.Name, manager.Observed(() => person.Partner.Name));
        }
        manager.NotifyCycleEnded();
        foreach (var person in people.Where((_, i) => i % 2 == 0))
            Assert.AreEqual(person.Partner!.Name, manager.Observed(() => person.Partner.Name));
        manager.NotifyCycleEnded();
        for (var i = 0; i < people.Count; ++i)
        {
            Assert.AreEqual(i % 2 == 0 ? 1 : 0, people[i].PropertyChangedSubscribers, $"person {i}");
            Assert.AreEqual(i % 2 == 0 ? 1 : 0, people[i].Partner!.PropertyChangedSubscribers, $"partner {i}");
        }
        people[0].Name = "Unobserved";
        Assert.AreEqual(0, component.StateHasChangedCalls);
        people[0].Partner!.Name = "Observed";
        Assert.AreEqual(1, component.StateHasChangedCalls);
        foreach (var person in people)
            Assert.AreEqual(person.Partner!.Name, manager.Observed(() => person.Partner.Name));
        manager.NotifyCycleEnded();
        for (var i = 0; i < people.Count; ++i)
            Assert.AreEqual(1, people[i].Partner!.PropertyChangedSubscribers, $"partner {i} after returning");
    }

    [TestMethod]
    public void NothingIsReleasedWhileEveryObservationIsUsedAgain()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada", Partner = new Person { Name = "Grace" } };
        for (var i = 0; i < 5; ++i)
        {
            manager.Observed(() => person.Name);
            manager.Observed(() => person.Partner.Name);
            manager.NotifyCycleEnded();
        }
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        Assert.AreEqual(1, person.Partner.PropertyChangedSubscribers);
    }

    [TestMethod]
    public void WhatACycleDidNotUseIsReleasedWhenItEnds()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada", Age = 36 };
        manager.Observed(() => person.Name);
        manager.Observed(() => person.Age);
        manager.NotifyCycleEnded();
        manager.Observed(() => person.Name);
        manager.NotifyCycleEnded();
        person.Age = 85;
        Assert.AreEqual(0, component.StateHasChangedCalls);
        person.Name = "Grace";
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void WhatEveryCycleStoppedUsingIsUnsubscribed()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        manager.Observed(() => person.Name);
        manager.NotifyCycleEnded();
        manager.NotifyCycleEnded();
        Assert.AreEqual(0, person.PropertyChangedSubscribers);
    }

    [TestMethod]
    public void WhatWasObservedBeforeACycleEndedSurvivesThatEnd()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        manager.Observed(() => person.Name);
        manager.NotifyCycleEnded();
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        person.Name = "Grace";
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }
}
