namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class ObservedValues
{
    [TestMethod]
    public void AChangeFromAnotherThreadReachesTheComponent()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        manager.Observed(() => person.Name);
        Task.Run(() => person.Name = "Grace").Wait();
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AChangeToAnIntermediatePropertyNotifies()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Name = "Grace" } };
        manager.Observed(() => person.Partner.Name);
        person.Partner = new Person { Name = "Hedy" };
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AChangeToAnObservedPropertyNotifies()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        manager.Observed(() => person.Name);
        person.Name = "Grace";
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AChangeToAnotherPropertyDoesNotNotify()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        manager.Observed(() => person.Name);
        person.Age = 36;
        Assert.AreEqual(0, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AConstantIsReturnedWithoutSubscribing()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person();
        Assert.AreSame(person, manager.Observed(() => person));
        Assert.AreEqual(0, person.PropertyChangedSubscribers);
    }

    [TestMethod]
    public void AFieldAlongTheChainIsRead()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Nickname = "Amazing Grace" } };
        Assert.AreEqual("Amazing Grace", manager.Observed(() => person.Partner.Nickname));
    }

    [TestMethod]
    public void AMethodCallIsRefused()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        Assert.ThrowsException<ArgumentException>(() => manager.Observed(() => person.ToString()));
        Assert.ThrowsException<ArgumentException>(() => manager.Observed(() => person.Name!.ToUpperInvariant().Length));
    }

    [TestMethod]
    public void ANullIntermediateIsRefusedAfterSubscribingWhatPrecedesIt()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person();
        var exception = Assert.ThrowsException<ArgumentException>(() => manager.Observed(() => person.Partner!.Name));
        StringAssert.StartsWith(exception.Message, "Expression path is not resolvable due to some part of it being null.");
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        person.Partner = new Person();
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void ANullPropertyNameNotifies()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        manager.Observed(() => person.Name);
        person.Raise(null);
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void ANullRootIsRefused()
    {
        var (manager, _) = Managers.CreateInitialized();
        Person? nobody = null;
        Assert.ThrowsException<ArgumentException>(() => manager.Observed(() => nobody!.Name));
    }

    [TestMethod]
    public void AnEmptyPropertyNameNotifies()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        manager.Observed(() => person.Name);
        person.Raise(string.Empty);
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AnOperatorIsRefused()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Age = 36 };
        Assert.ThrowsException<ArgumentException>(() => manager.Observed(() => person.Age + 1));
    }

    [TestMethod]
    public void AStaticRootIsRefused()
    {
        var (manager, _) = Managers.CreateInitialized();
        Assert.ThrowsException<ArgumentException>(() => manager.Observed(() => StaticHolder.Person.Name));
    }

    [TestMethod]
    public void AStructAlongTheChainIsRead()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Position = new Point(3, 4) } };
        Assert.AreEqual(4, manager.Observed(() => person.Partner.Position.Y));
    }

    [TestMethod]
    public void AValueTypedPropertyIsReturned()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Age = 36 };
        Assert.AreEqual(36, manager.Observed(() => person.Age));
    }

    [TestMethod]
    public void ABoxingConversionIsRefused()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Age = 36 };
        Assert.ThrowsException<ArgumentException>(() => manager.Observed<object>(() => person.Age));
    }

    [TestMethod]
    public void AValueIsReturnedThroughAReferenceConversion()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        Assert.AreEqual("Ada", manager.Observed<object?>(() => person.Name));
        Assert.AreEqual("Ada", manager.Observed(() => person.Name));
        person.Name = "Grace";
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AnExceptionFromAGetterAlongTheChainArrivesAsThrown()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new CountingPerson();
        var exception = Assert.ThrowsException<InvalidOperationException>(() => manager.Observed(() => person.Throwing.Name));
        Assert.AreEqual("thrown by a getter", exception.Message);
    }

    [TestMethod]
    public void EachMemberAlongTheChainIsReadOncePerCall()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new CountingPerson { Partner = new Person { Name = "Grace" } };
        Assert.AreEqual("Grace", manager.Observed(() => person.Partner.Name));
        Assert.AreEqual(1, person.PartnerReads);
        Assert.AreEqual("Grace", manager.Observed(() => person.Partner.Name));
        Assert.AreEqual(2, person.PartnerReads);
    }

    [TestMethod]
    public void ManyMembersOfOneObjectAreObservedApart()
    {
        var (manager, component) = Managers.CreateInitialized();
        var wide = new Wide { P0 = 0, P1 = 1, P2 = 2, P3 = 3, P4 = 4, P5 = 5, P6 = 6, P7 = 7, P8 = 8, P9 = 9, P10 = 10, P11 = 11 };
        var read = new[] { manager.Observed(() => wide.P0), manager.Observed(() => wide.P1), manager.Observed(() => wide.P2), manager.Observed(() => wide.P3), manager.Observed(() => wide.P4), manager.Observed(() => wide.P5), manager.Observed(() => wide.P6), manager.Observed(() => wide.P7), manager.Observed(() => wide.P8), manager.Observed(() => wide.P9), manager.Observed(() => wide.P10), manager.Observed(() => wide.P11) };
        CollectionAssert.AreEqual(Enumerable.Range(0, 12).ToArray(), read);
        Assert.AreEqual(1, wide.PropertyChangedSubscribers);
        wide.P11 = 111;
        Assert.AreEqual(1, component.StateHasChangedCalls);
        Assert.AreEqual(111, manager.Observed(() => wide.P11));
        Assert.AreEqual(10, manager.Observed(() => wide.P10));
    }

    [TestMethod]
    public void ObservingTheSameChainAgainAllocatesNothing()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada", Age = 36, Partner = new Person { Name = "Grace" } };
        Expression<Func<string?>> name = () => person.Partner.Name;
        Expression<Func<int>> age = () => person.Age;
        void ObserveAndEndCycle()
        {
            manager.Observed(name);
            manager.Observed(age);
            manager.NotifyCycleEnded();
        }
        for (var i = 0; i < 100; ++i)
            ObserveAndEndCycle();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; ++i)
            ObserveAndEndCycle();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
#if WITHOUT_DYNAMIC_CODE
        Assert.AreEqual(24 * 1000, allocated, "reading a value type through reflection boxes it, which should be the only allocation");
#else
        Assert.AreEqual(0, allocated);
#endif
    }

    [TestMethod]
    public void ObservingTheSameChainAgainDoesNotSubscribeAgain()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Name = "Grace" } };
        for (var i = 0; i < 3; ++i)
            manager.Observed(() => person.Partner.Name);
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        Assert.AreEqual(1, person.Partner.PropertyChangedSubscribers);
    }

    [TestMethod]
    public void ObservingTwoPropertiesOfOneObjectSubscribesOnce()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada", Age = 36 };
        manager.Observed(() => person.Name);
        manager.Observed(() => person.Age);
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        person.Name = "Grace";
        person.Age = 85;
        Assert.AreEqual(2, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void ReturnsTheValueAtTheEndOfTheChain()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Name = "Grace" } };
        Assert.AreEqual("Grace", manager.Observed(() => person.Partner.Name));
    }

    [TestMethod]
    public void SubscribesToEveryObjectAlongTheChain()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Partner = new Person { Name = "Grace" } };
        manager.Observed(() => person.Partner.Name);
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        Assert.AreEqual(1, person.Partner.PropertyChangedSubscribers);
    }

    [TestMethod]
    public void TheValueIsReadAfterTheSubscription()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new RacingPerson();
        person.Reading = () => person.Raise(nameof(RacingPerson.Racy));
        manager.Observed(() => person.Racy);
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }
}
