namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

public class TestTeam :
    PropertyChangeNotifier,
    IComparable<TestTeam>
{
    public TestTeam() :
        this(new RangeObservableCollection<TestPerson>())
    {
    }

    public TestTeam(RangeObservableCollection<TestPerson>? people) =>
        this.people = people;

    RangeObservableCollection<TestPerson>? people;

    public int CompareTo(TestTeam? other) =>
        GetHashCode().CompareTo(other?.GetHashCode() ?? 0);

    public RangeObservableCollection<TestPerson>? People
    {
        get => people;
        set => SetBackedProperty(ref people, in value);
    }
}
