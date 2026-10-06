namespace Epiforge.Extensions.Blazor.Tests;

[TestClass]
public class SharedShapes
{
    sealed class Holder(IReactivityManager manager, Person member)
    {
        public Person Member { get; } = member;

        public string? ReadThroughThis() =>
            manager.Observed(() => Member.Name);
    }

    static string? ReadName(IReactivityManager manager, Person person) =>
        manager.Observed(() => person.Name);

    [TestMethod]
    public void AChainThroughTheComponentItselfIsObserved()
    {
        var (manager, component) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        var holder = new Holder(manager, person);
        Assert.AreEqual("Ada", holder.ReadThroughThis());
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
        person.Name = "Grace";
        Assert.AreEqual(1, component.StateHasChangedCalls);
    }

    [TestMethod]
    public void AnAnonymousObjectIsResolvedLikeAClosure()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        var anonymous = new { Person = person };
        Assert.AreEqual("Ada", manager.Observed(() => anonymous.Person.Name));
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
    }

    [TestMethod]
    public void ANestedClosureIsResolved()
    {
        var (manager, _) = Managers.CreateInitialized();
        var outer = new Person { Name = "Ada" };
        string? Read()
        {
            var inner = new Person { Name = "Grace" };
            return manager.Observed(() => outer.Name) + manager.Observed(() => inner.Name);
        }
        Assert.AreEqual("AdaGrace", Read());
        Assert.AreEqual("AdaGrace", Read());
        Assert.AreEqual(1, outer.PropertyChangedSubscribers);
    }

    [TestMethod]
    public void OneRootReachedThroughDifferentClosuresIsObservedOnce()
    {
        var (manager, _) = Managers.CreateInitialized();
        var person = new Person { Name = "Ada" };
        for (var i = 0; i < 3; ++i)
        {
            ReadName(manager, person);
            manager.NotifyCycleEnded();
        }
        Assert.AreEqual(1, person.PropertyChangedSubscribers);
    }

    [TestMethod]
    public void TwoRootsSharingAShapeEachSeeTheirOwnValues()
    {
        var (manager, component) = Managers.CreateInitialized();
        var people = Enumerable.Range(0, 4).Select(i => new Person { Name = $"Person {i}" }).ToList();
        CollectionAssert.AreEqual(people.Select(person => person.Name).ToList(), people.Select(person => ReadName(manager, person)).ToList());
        foreach (var person in people)
            Assert.AreEqual(1, person.PropertyChangedSubscribers);
        people[2].Name = "Changed";
        Assert.AreEqual(1, component.StateHasChangedCalls);
        Assert.AreEqual("Changed", ReadName(manager, people[2]));
        Assert.AreEqual("Person 1", ReadName(manager, people[1]));
    }
}
