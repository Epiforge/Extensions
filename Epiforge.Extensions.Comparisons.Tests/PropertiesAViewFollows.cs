namespace Epiforge.Extensions.Comparisons.Tests;

/// <summary>
/// Which announcements of an element's properties a filtered view acts on: one naming no property, as <see cref="PropertyChangedEventArgs"/> allows with <see langword="null"/> or <see cref="string.Empty"/> to mean every property may have changed, and one naming a property the predicate reads but the view was not told of
/// </summary>
/// <remarks>
/// The element's rank moves from 1 to 10 without an announcement naming it, and the view keeps elements ranked above 5
/// </remarks>
[TestClass]
public class PropertiesAViewFollows
{
    static readonly Expression<Func<Person, bool>> aboveFive = person => person.Rank > 5;
    static readonly Func<Person, bool> aboveFiveDelegate = aboveFive.Compile();

    static void ChangeWithoutNaming(ObservableCollection<Person> people, string? propertyName)
    {
        people[0].SetRankSilently(10);
        people[0].AnnounceEveryProperty(propertyName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void DynamicDataFollowsAnAnnouncementNamingNoPropertyWhenToldToRefreshOnAny(string? propertyName)
    {
        var people = Person.Many(1, 2);
        using var subscription = people.ToObservableChangeSet().AutoRefresh().Filter(aboveFiveDelegate).Bind(out var view).Subscribe();
        ChangeWithoutNaming(people, propertyName);
        Assert.AreEqual("P0", Person.Names(view));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void DynamicDataIgnoresAnAnnouncementNamingNoPropertyWhenToldWhichPropertyToRefreshOn(string? propertyName)
    {
        var people = Person.Many(1, 2);
        using var subscription = people.ToObservableChangeSet().AutoRefresh(person => person.Rank).Filter(aboveFiveDelegate).Bind(out var view).Subscribe();
        ChangeWithoutNaming(people, propertyName);
        Assert.AreEqual(string.Empty, Person.Names(view));
    }

    [TestMethod]
    public void DynamicDataIgnoresAPropertyThePredicateReadsWhenNotToldOfIt()
    {
        var people = Person.Many(1, 2);
        using var subscription = people.ToObservableChangeSet().AutoRefresh(person => person.Score).Filter(aboveFiveDelegate).Bind(out var view).Subscribe();
        people[0].Rank = 10;
        Assert.AreEqual(string.Empty, Person.Names(view));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ExpressionsFollowsAnAnnouncementNamingNoProperty(string? propertyName)
    {
        var people = Person.Many(1, 2);
        using var observation = new CollectionObserver().ObserveReadOnlyList(people);
        using var view = observation.ObserveWhere(aboveFive);
        ChangeWithoutNaming(people, propertyName);
        Assert.AreEqual("P0", Person.Names(view));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void NmfIgnoresAnAnnouncementNamingNoProperty(string? propertyName)
    {
        var people = Person.Many(1, 2);
        using var view = ((IEnumerable<Person>)people).WithUpdates().Where(aboveFive);
        view.Successors.SetDummy();
        ChangeWithoutNaming(people, propertyName);
        Assert.AreEqual(string.Empty, Person.Names(view));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ObservableComputationsIgnoresAnAnnouncementNamingNoProperty(string? propertyName)
    {
        var people = Person.Many(1, 2);
        using var consumer = new OcConsumer();
        var view = people.Filtering(aboveFive).For(consumer);
        ChangeWithoutNaming(people, propertyName);
        Assert.AreEqual(string.Empty, Person.Names(view));
    }
}
